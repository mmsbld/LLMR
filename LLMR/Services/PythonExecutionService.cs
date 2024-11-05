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
    private readonly Exception? _threadException;

    private readonly TaskCompletionSource<bool> _initTcs = new();
    public Task<bool> InitializationTask => _initTcs.Task;

    public event EventHandler<string>? ExceptionOccurred;
    public event EventHandler<string>? ConsoleMessageOccurred;

    // private constructor
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
            if (_instance == null || !_instance._isPythonInitialized || !_instance.InitializationTask.IsCompleted || !_instance.InitializationTask.Result)
            {
                _instance?.Dispose();
                _instance = new PythonExecutionService(pythonPath);
            }
            var instancePythonPath = _instance._pythonPath ?? throw new NullReferenceException("dere_instance._pythonPath is null");
            if (_instance._pythonPath.Equals(pythonPath, StringComparison.OrdinalIgnoreCase)) return _instance;
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
                    throw new DirectoryNotFoundException($"<PES.cs error> Invalid Python path. The specified path '{_pythonPath}' does not exist.");
                }

                var pythonExecutable = Path.Combine(_pythonPath, "3.12", "bin", "python3.12");
                if (!File.Exists(pythonExecutable))
                {
                    throw new FileNotFoundException($"<PES.cs error> Python executable not found at '{pythonExecutable}'.");
                }

                // Get Python version and dynamic library path
                var (pythonVersion, pythonDllPath) = GetPythonVersionAndDllPath(pythonExecutable);

                ConsoleMessageOccurred?.Invoke(this, $"Detected Python version: {pythonVersion}.");
                ConsoleMessageOccurred?.Invoke(this, $"Python dynamic library path: {pythonDllPath}");

                // Set Runtime.PythonDLL
                Runtime.PythonDLL = pythonDllPath;

                // Set PythonEngine.PythonHome
                var pythonHome = Path.Combine(_pythonPath, "3.12");
                PythonEngine.PythonHome = pythonHome;

                // Set PythonEngine.PythonPath
                string stdLibPath = Path.Combine(pythonHome, "lib", $"python{pythonVersion}");
                string sitePackagesPath = Path.Combine(stdLibPath, "site-packages");
                PythonEngine.PythonPath = string.Join(
                    Path.PathSeparator.ToString(),
                    new string[] { stdLibPath, sitePackagesPath }
                );

                ConsoleMessageOccurred?.Invoke(this, $"Set PythonEngine.PythonHome to '{PythonEngine.PythonHome}'.");
                ConsoleMessageOccurred?.Invoke(this, $"Set PythonEngine.PythonPath to '{PythonEngine.PythonPath}'.");

                // Initialize the Python engine
                PythonEngine.Initialize();
                ConsoleMessageOccurred?.Invoke(this, "PythonEngine initialized successfully.");
                _isPythonInitialized = true;
                _initTcs.TrySetResult(true);

                using (Py.GIL())
                {
                    dynamic sys = Py.Import("sys");
                    sys.path.append(stdLibPath);
                    sys.path.append(sitePackagesPath);
                    ConsoleMessageOccurred?.Invoke(this, $"Appended '{stdLibPath}' and '{sitePackagesPath}' to sys.path.");



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
                    _instance = null;
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
            dynamic importlib_metadata = Py.Import("importlib.metadata");

            string[] requiredPackages = { "requests", "gradio", "openai" };
            string[] requiredVersions = { "2.31", "5.1", "1.52" };

            for (var i = 0; i < requiredPackages.Length; i++)
            {
                var package = requiredPackages[i];
                var requiredVersion = requiredVersions[i];
                try
                {
                    string installedVersion = importlib_metadata.version(package);

                    ConsoleMessageOccurred?.Invoke(this, $"Checking package '{package}'. Installed version: {installedVersion}, Required version: {requiredVersion}.");
                    
                    if (!installedVersion.StartsWith(requiredVersion)) {
                        throw new InvalidOperationException($"<PES.cs (!) error: > Package '{package}' version {installedVersion} does not match required version prefix {requiredVersion}.");
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"<PES.cs (!) error: > Loading required package '{package}' leads to problems: {ex.Message}.");
                }
            }
        }
    }
    
    private (string Version, string PythonDllPath) GetPythonVersionAndDllPath(string pythonExecutable)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = "-c \"import sys; import sysconfig; print(sys.version_info[0:2]); print(sysconfig.get_config_var('LIBDIR')); print(sysconfig.get_config_var('LDLIBRARY'))\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            throw new InvalidOperationException("<PES.cs error> Failed to start Python process.");
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 3)
        {
            throw new InvalidOperationException("<PES.cs error> Unable to get Python version and library info.");
        }

        // Extract major and minor version
        var versionInfo = lines[0].Trim('(', ')').Split(',');
        string major = versionInfo[0].Trim();
        string minor = versionInfo[1].Trim();
        string version = $"{major}.{minor}";

        string libDir = lines[1].Trim();
        string ldLibrary = lines[2].Trim();

        string pythonDllPath = Path.Combine(libDir, ldLibrary);

        return (version, pythonDllPath);
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
