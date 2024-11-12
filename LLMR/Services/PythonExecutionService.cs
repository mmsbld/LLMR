using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Python.Runtime;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LLMR.Helpers;

namespace LLMR.Services;

public class PythonExecutionService : IDisposable
{
    private static readonly object Lock = new();
    private static PythonExecutionService? _instance;

    private readonly Thread _pythonThread;
    private readonly BlockingCollection<Func<Task>?> _taskQueue = new();
    private bool _isDisposed;
    private bool _isPythonInitialized;
    private readonly string _pythonPath;

    private readonly TaskCompletionSource<bool> _initTcs = new();
    public Task<bool> InitializationTask => _initTcs.Task;

    public event EventHandler<string>? ExceptionOccurred;
    public event EventHandler<string>? ConsoleMessageOccurred;

    private PythonExecutionService(string pythonPath)
    {
        if (string.IsNullOrEmpty(pythonPath))
            throw new ArgumentNullException(nameof(pythonPath));

        _pythonPath = pythonPath;
        _pythonThread = new Thread(PythonThreadStart) { IsBackground = true };
        _pythonThread.Start();
    }

    public static PythonExecutionService GetInstance(string pythonPath)
    {
        if (string.IsNullOrEmpty(pythonPath))
            throw new ArgumentNullException(nameof(pythonPath), "<PES.cs error> Python path cannot be null or empty.");

        lock (Lock)
        {
            if (_instance == null || !_instance._isPythonInitialized || !_instance.InitializationTask.IsCompleted || !_instance.InitializationTask.Result)
            {
                _instance?.Dispose();
                _instance = new PythonExecutionService(pythonPath);
            }
            else if (!_instance._pythonPath.Equals(pythonPath, StringComparison.OrdinalIgnoreCase))
            {
                _instance.Dispose();
                _instance = new PythonExecutionService(pythonPath);
            }
            return _instance;
        }
    }


    private void PythonThreadStart()
    {
        try
        {
            var baseDataDir = AppDataPath.GetBaseDataDirectory();
            ConsoleMessageOccurred?.Invoke(this, $"<PES.cs> Base data directory: {baseDataDir}");
            //ExtractEmbeddedScripts(baseDataDir);

            if (!Directory.Exists(_pythonPath))
                throw new DirectoryNotFoundException($"<PES.cs error> Invalid Python path: {_pythonPath}");

            string pythonExecutable;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //pythonExecutable = File.Exists(_pythonPath) ? _pythonPath : Path.Combine(_pythonPath, "envs", "myenv", "python.exe");
                if (File.Exists(_pythonPath))
                {
                    // if _pythonPath is already pointing to python.exe
                    pythonExecutable = _pythonPath;
                }
                else if (Directory.Exists(_pythonPath))
                {
                    // if _pythonPath points to the env directory 
                    pythonExecutable = Path.Combine(_pythonPath, "python.exe");
                }
                else
                {
                    throw new DirectoryNotFoundException($"<PES.cs error> Invalid Python path: {_pythonPath}");
                }
            }
            else
            {
                pythonExecutable = Path.Combine(_pythonPath, "envs", "myenv", "bin", "python3.12");
            }

            ConsoleMessageOccurred?.Invoke(this, $"Python Executable Path: {pythonExecutable}");

            if (!File.Exists(pythonExecutable))
                throw new FileNotFoundException($"<PES.cs error> Python executable not found at {pythonExecutable}");

            var (pythonVersion, pythonDllPath) = GetPythonVersionAndDllPath(pythonExecutable);

            ConsoleMessageOccurred?.Invoke(this, $"Detected Python version: {pythonVersion}");
            ConsoleMessageOccurred?.Invoke(this, $"Python DLL Path: {pythonDllPath}");

            if (!File.Exists(pythonDllPath))
                throw new FileNotFoundException($"<PES.cs error> Python dynamic library not found at '{pythonDllPath}'.");

            Runtime.PythonDLL = pythonDllPath;
            Environment.SetEnvironmentVariable("PATH", $"{Path.GetDirectoryName(pythonDllPath)};{Environment.GetEnvironmentVariable("PATH")}");
            PythonEngine.Initialize();
            ConsoleMessageOccurred?.Invoke(this, "PythonEngine initialized successfully.");
            _isPythonInitialized = true;
            _initTcs.TrySetResult(true);

            using (Py.GIL())
{

    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
    
    ConsoleMessageOccurred?.Invoke(this, $"AppDomain Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
    ConsoleMessageOccurred?.Invoke(this, $"Current Directory: {Directory.GetCurrentDirectory()}");
    ConsoleMessageOccurred?.Invoke(this, $"Temp Path: {Path.GetTempPath()}");
    ConsoleMessageOccurred?.Invoke(this, $"Assembly Location: {System.Reflection.Assembly.GetExecutingAssembly().Location}");

    ConsoleMessageOccurred?.Invoke(this, $"<PES.cs> AppDataPath.GetBaseDataDirectory() is {AppDataPath.GetBaseDataDirectory()}");
    dynamic sys = Py.Import("sys");
    var pythonV = "3.12"; // adjust to correct mac PATH (name of version folder)

    // path handling here:
    var pythonDir = _pythonPath;
    if (pythonDir == null)
        throw new InvalidOperationException("<PES.cs error> Unable to determine Python executable directory.");

    var scriptsPath = Path.Combine(pythonDir, "Lib", $"python{pythonV}");
    var sitePackagesPath = Path.Combine(pythonDir, "Lib", "site-packages");
    
    if (Directory.Exists(scriptsPath))
    {
        ConsoleMessageOccurred?.Invoke(this, $"Appending to sys.path: {scriptsPath}");
        sys.path.append(scriptsPath);
    }
    else
    {
        ConsoleMessageOccurred?.Invoke(this, $"Warning: scriptsPath does not exist: {scriptsPath}");
    }

    if (Directory.Exists(sitePackagesPath))
    {
        ConsoleMessageOccurred?.Invoke(this, $"Appending to sys.path: {sitePackagesPath}");
        sys.path.append(sitePackagesPath);
    }
    else
    {
        ConsoleMessageOccurred?.Invoke(this, $"Warning: sitePackagesPath does not exist: {sitePackagesPath}");
    }

    var scriptsDir = Path.Combine(baseDir, "Scripts");
    if (Directory.Exists(scriptsDir))
    {
        ConsoleMessageOccurred?.Invoke(this, $"Appending to sys.path: {scriptsDir}");
        sys.path.append(scriptsDir);
    }
    else
    {
        ConsoleMessageOccurred?.Invoke(this, $"Warning: scriptsDir does not exist: {scriptsDir}");
    }

    const string redirectScript = @"
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
                var taskFunc = _taskQueue.Take();
                taskFunc?.Invoke().Wait();
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
                    ConsoleMessageOccurred?.Invoke(this, "<PES.cs> PythonEngine was already initialized.");
                    ConsoleMessageOccurred?.Invoke(this, "<PES.cs> PythonEngine.Shutdown() called --> BinaryFormatter exceptions expected!");
                    PythonEngine.Shutdown();
                    ConsoleMessageOccurred?.Invoke(this, "<PES.cs> Application must be restarted. PythonEngine was already initialized.");
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

    public Task<T> ExecuteAsync<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();

        _taskQueue.Add(async () =>
        {
            using (Py.GIL())
            {
                try
                {
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
            await Task.CompletedTask;
        });

        return tcs.Task;
    }

    private void VerifyRequiredPackages()
    {
        using (Py.GIL())
        {
            string[] requiredPackages = { "requests", "openai", "gradio" };
            string[] requiredVersions = { "2.31", "1.54", "5.1" };

            for (var i = 0; i < requiredPackages.Length; i++)
            {
                var package = requiredPackages[i];
                var requiredVersion = requiredVersions[i];
                try
                {
                    dynamic pkg = Py.Import(package);
                    string installedVersion = pkg.__version__;
                    ConsoleMessageOccurred?.Invoke(this, $"<PES.cs> Checking package '{package}'. Installed version: {installedVersion}, Required version: {requiredVersion}");

                    if (!installedVersion.StartsWith(requiredVersion))
                        throw new InvalidOperationException($"<PES.cs error> Package '{package}' version {installedVersion} does not match required version prefix {requiredVersion}.");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"<PES.cs error> Loading required package '{package}' leads to problems: {ex.Message}.");
                }
            }
        }
    }


    private (string Version, string PythonDllPath) GetPythonVersionAndDllPath(string pythonExecutable)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = "-c \"import sys; import sysconfig; print(sys.version_info[0:2]); print(sysconfig.get_config_var('LIBDIR')); print(sysconfig.get_config_var('LDLIBRARY')); print(sysconfig.get_config_var('INSTSONAME'))\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("<PES.cs error> Failed to start Python process.");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 4)
            throw new InvalidOperationException("<PES.cs error> Unable to get Python version and library info.");

        for(var i = 0; i < lines.Length; i++)
        {
            ConsoleMessageOccurred?.Invoke(this, $"<PES.cs> lines[{i}]: {lines[i]}");
        }

        var versionInfo = lines[0].Trim('(', ')').Split(',');
        var major = versionInfo[0].Trim();
        var minor = versionInfo[1].Trim().TrimEnd(')');
        var version = $"{major}.{minor}";

        ConsoleMessageOccurred?.Invoke(this, $"<PES.cs> versionInfo: {string.Join(", ", versionInfo)}");
        ConsoleMessageOccurred?.Invoke(this, $"<PES.cs> major: {major}");
        ConsoleMessageOccurred?.Invoke(this, $"<PES.cs> minor: {minor}");
        ConsoleMessageOccurred?.Invoke(this, $"<PES.cs> version: {version}");

        string pythonDllPath, libDir, pythonDllName;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            libDir = lines[1].Trim();
            if (libDir == "None")
            {
                libDir = Path.GetDirectoryName(pythonExecutable) ?? throw new InvalidOperationException("<PES.cs error> Unable to determine Python executable directory.");
            }
            var instsOname = lines[3].Trim();
            if (!string.IsNullOrEmpty(instsOname) && instsOname != "None")
            {
                pythonDllName = instsOname.TrimEnd(')');
            }
            else
            {
                pythonDllName = $"python{major}{minor}.dll";
            }
            pythonDllPath = Path.Combine(libDir, pythonDllName);
        }
        else
        {
            libDir = lines[1].Trim();
            pythonDllName = $"libpython{version}.dylib";
            pythonDllPath = Path.Combine(libDir, pythonDllName);
        }

        ConsoleMessageOccurred?.Invoke(this, $"<PES.cs> LIBDIR (!-- PATH --!): {libDir} (!-- PATH --!)");
        ConsoleMessageOccurred?.Invoke(this, $"<PES.cs> Python DLL Name (!-- PATH --!): {pythonDllName} (!-- PATH --!)");
        ConsoleMessageOccurred?.Invoke(this, $"<PES.cs> Python DLL Path (!-- PATH --!): {pythonDllPath} (!-- PATH --!)");

        if (!File.Exists(pythonDllPath))
            throw new FileNotFoundException($"<PES.cs error> Python dynamic library not found at '{pythonDllPath}'.");

        return (version, pythonDllPath);
    }
    
    // ReSharper disable once UnusedMember.Global
    public static void PythonStdout(string message)
    {
        _instance?.ConsoleMessageOccurred?.Invoke(_instance, $"<PES stdout> {message}");
    }
    // ReSharper disable once UnusedMember.Global
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
        catch { }
        lock (Lock)
        {
            _instance = null;
        }
    }
}