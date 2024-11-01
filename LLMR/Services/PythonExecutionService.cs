using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Python.Runtime;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;

namespace LLMR.Services;

public class PythonExecutionService : IDisposable
{
    private static readonly object Lock = new();
    private static PythonExecutionService? _instance;

    private readonly Thread _pythonThread;
    private readonly BlockingCollection<Func<Task>?> _taskQueue = new();
    private bool _isDisposed;
    private bool _isPythonInitialized;
    private readonly string? _pythonPath;
    private readonly Exception? _threadException; // exceptions thrown in background thread

    private readonly TaskCompletionSource<bool> _initTcs = new();
    public Task<bool> InitializationTask => _initTcs.Task;

    public event EventHandler<string>? ExceptionOccurred;
    public event EventHandler<string>? ConsoleMessageOccurred;
    
    // private constructor (-> singleton, GoF)
    private PythonExecutionService(string? pythonPath)
    {
        _threadException = null;
        ArgumentNullException.ThrowIfNull(pythonPath);
        _pythonPath = pythonPath;

        _pythonThread = new Thread(PythonThreadStart)
        {
            IsBackground = true
        };
        _pythonThread.Start();
    }

    // public getter for singleton
    public static PythonExecutionService GetInstance(string? pythonPath)
    {
        lock (Lock)
        {
            // if no instance exists (logical or) the existing instance failed to initialize (logical or) a different python path is provided
            if (_instance == null || !_instance._isPythonInitialized || !_instance.InitializationTask.IsCompleted || !_instance.InitializationTask.Result)
            {
                _instance?.Dispose();
                _instance = new PythonExecutionService(pythonPath);
            }
            var instancePythonPath = _instance._pythonPath ?? throw new NullReferenceException("dere_instance._pythonPath is null");
            if (_instance._pythonPath.Equals(pythonPath, StringComparison.OrdinalIgnoreCase)) return _instance;
            // if different python path is provided --> dispose current singleton and create a new one (potentially a bad or at least useless idea!)
            _instance.Dispose();
            _instance = new PythonExecutionService(pythonPath);
            return _instance;
        }
    }

    private void PythonThreadStart()
    {
        try
        {
            if (!Directory.Exists(_pythonPath))
            {
                throw new DirectoryNotFoundException($"<PES.cs (!) error: > Invalid Python path. The specified path '{_pythonPath}' does not exist or is incorrect. Ensure it contains the correct Python installation.");
            }

            ConsoleMessageOccurred?.Invoke(this, $"Setting PYTHONHOME to '{_pythonPath}'.");

            Environment.SetEnvironmentVariable("PYTHONHOME", _pythonPath);

            var pythonVersion = GetPythonVersion();

            var currentVersion = Version.Parse(pythonVersion);
            var minRequiredVersion = new Version(3, 12);
            ConsoleMessageOccurred?.Invoke(this, $"Installed Python version: {currentVersion}. Minimum required version: {minRequiredVersion}.");

            if (currentVersion < minRequiredVersion)
            {
                throw new InvalidOperationException($"<PES.cs (!) error: > Python version {currentVersion} is too low. The minimum required version is {minRequiredVersion}.");
            }

            var majorMinor = $"{currentVersion.Major}.{currentVersion.Minor}"; // Extract major.minor

            string dllName;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                string newPath = $"{_pythonPath};{path}";
                Environment.SetEnvironmentVariable("PATH", newPath);
                        
                dllName = $"python{currentVersion.Major}{currentVersion.Minor}.dll"; // e.g. python312.dll
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", Path.Combine(_pythonPath, "lib"));
                dllName = $"libpython{majorMinor}.so"; // e.g. libpython3.12.so
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Environment.SetEnvironmentVariable("DYLD_LIBRARY_PATH", Path.Combine(_pythonPath, "lib"));
                dllName = $"libpython{majorMinor}.dylib"; // e.g. libpython3.12.dylib
            }
            else
            {
                throw new PlatformNotSupportedException("<PES.cs (!) error: > Unsupported operating system.");
            }

            var pythonDllPath = Path.Combine(_pythonPath, "lib", dllName);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                pythonDllPath = Path.Combine(_pythonPath, dllName);
            }

            ConsoleMessageOccurred?.Invoke(this, $"Attempting to open dynamic library: '{pythonDllPath}'.");

            if (!File.Exists(pythonDllPath))
            {
                throw new FileNotFoundException($"<PES.cs (!) error: > Python DLL not found at '{pythonDllPath}'. Ensure the correct Python version is installed and the DLL is present. Bitte überprüfen Sie den Pfad im Dateiexplorer.");
            }

            ConsoleMessageOccurred?.Invoke(this, $"Successfully found Python DLL at '{pythonDllPath}'.");

            Runtime.PythonDLL = pythonDllPath;

            PythonEngine.Initialize();
            ConsoleMessageOccurred?.Invoke(this, "PythonEngine initialized successfully.");
            _isPythonInitialized = true;
            _initTcs.TrySetResult(true);
            
            using (Py.GIL())
            {
                string redirectScript = @"
import sys
import clr
clr.AddReference('LLMR')
from LLMR.Services import PythonExecutionService

class StdOutRedirector:
    def write(self, message):
        PythonExecutionService.PythonStdout(message)

    def flush(self):
        pass

class StdErrRedirector:
    def write(self, message):
        PythonExecutionService.PythonStderr(message)

    def flush(self):
        pass

sys.stdout = StdOutRedirector()
sys.stderr = StdErrRedirector()
";
                PythonEngine.Exec(redirectScript);
            }

            VerifyRequiredPackages();

            while (!_isDisposed)
            {
                Func<Task>? taskFunc = null;

                try
                {
                    taskFunc = _taskQueue.Take();
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                if (taskFunc != null)
                {
                    taskFunc().Wait();
                }
            }
        }
        catch (Exception ex)
        {
            ExceptionOccurred?.Invoke(this, $"{ex.Message}\n{ex.StackTrace}");
            _initTcs.TrySetResult(false);
            _isDisposed = true; 
            lock (Lock)
            {
                _instance = null; // allow re-init on failure
            }
        }
        finally
        {
            if (_isPythonInitialized)
            {
                try
                {
                    PythonEngine.Shutdown();
                    ConsoleMessageOccurred?.Invoke(this, "PythonEngine shutdown successfully.");
                }
                catch (Exception ex)
                {
                    ExceptionOccurred?.Invoke(this, $"{ex.Message}\n{ex.StackTrace}");
                    _initTcs.TrySetResult(false);
                    _isDisposed = true; 
                    lock (Lock)
                    {
                        _instance = null;
                    }
                }
            }
        }
    }

    private void CheckForThreadException()
    {
        if (_threadException != null)
        {
            throw _threadException;
        }
    }

    public Task<T> ExecuteAsync<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();

        _taskQueue.Add(() =>
        {
            using (Py.GIL())
            {
                try
                {
                    CheckForThreadException(); 
                    var result = func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    ExceptionOccurred?.Invoke(this, $"{ex.Message}\n{ex.StackTrace}");
                    _initTcs.TrySetResult(false);
                    _isDisposed = true; 
                    lock (Lock)
                    {
                        _instance = null;
                    }
                    tcs.SetException(ex);
                }
            }

            return Task.CompletedTask;
        });

        return tcs.Task;
    }

    private string GetPythonVersion()
    {
        if (_pythonPath == null)
            throw new NullReferenceException("Python path is null.");
        var pythonExecutable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(_pythonPath, "python.exe") : Path.Combine(_pythonPath, "bin", "python3");

        if (!File.Exists(pythonExecutable))
        {
            throw new FileNotFoundException($"<PES.cs (!) error: > Python executable not found in '{pythonExecutable}'. Ensure the correct Python version is installed.");
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = "--version",
            RedirectStandardOutput = true,
            RedirectStandardError = true, // capture version info from stderr if needed (do not forget the -u calls in the scripts!)
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            throw new InvalidOperationException("<PES.cs (!) error: > Failed to start Python process.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var errorOutput = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrEmpty(output))
        {
            // output version info to stderr
            output = errorOutput;
        }

        if (!output.ToLower().StartsWith("python"))
        {
            throw new InvalidOperationException("<PES.cs (!) error: > Unable to determine Python version.");
        }

        // should be something like this: "Python 3.12.4"
        var parts = output.Split(' ');
        if (parts.Length < 2)
        {
            throw new InvalidOperationException("<PES.cs (!) error: > Unable to parse Python version.");
        }

        ConsoleMessageOccurred?.Invoke(this, $"Detected Python version: {parts[1].Trim()}.");

        return parts[1].Trim();
    }

    private void VerifyRequiredPackages()
    {
        using (Py.GIL())
        {
            dynamic pip = Py.Import("pip");
            // ReSharper disable once InconsistentNaming
            dynamic pkg_resources = Py.Import("pkg_resources");

            string[] requiredPackages = { "requests", "gradio", "openai" };
            string[] versions = { "2.32.3", "5.1.0", "1.52.0" };

            for (var i = 0; i < requiredPackages.Length; i++)
            {
                var package = requiredPackages[i];
                var requiredVersion = versions[i];
                try
                {
                    dynamic dist = pkg_resources.get_distribution(package);
                    string installedVersion = dist.version;

                    ConsoleMessageOccurred?.Invoke(this, $"Checking package '{package}'. Installed version: {installedVersion}, Minimum required version: {requiredVersion}.");

                    if (Version.Parse(installedVersion) < Version.Parse(requiredVersion))
                    {
                        throw new InvalidOperationException($"<PES.cs (!) error: > Package '{package}' version {installedVersion} is too old. Minimum required version is {requiredVersion}.");
                    }
                }
                catch (Exception)
                {
                    throw new InvalidOperationException($"<PES.cs (!) error: > Required package '{package}' is not installed or outdated.");
                }
            }
        }
    }
    
    // ReSharper disable once UnusedMember.Global [is used in PythonThreadStart()].
    public static void PythonStdout(string message)
    {
        _instance?.ConsoleMessageOccurred?.Invoke(_instance, $"<PES stdout> {message}");
    }
    // ReSharper disable once UnusedMember.Global [is used in PythonThreadStart()].
    public static void PythonStderr(string message)
    {
        _instance?.ExceptionOccurred?.Invoke(_instance, $"<PES stderr> {message}");
    }
        
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _taskQueue.CompleteAdding();
        try
        {
            _pythonThread.Join();
        }
        catch
        {
            // suppress all exceptions during disposal 
        }
        lock (Lock)
        {
            _instance = null;
        }
    }
}