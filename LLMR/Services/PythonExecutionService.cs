using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Python.Runtime;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using LLMR.Helpers;

namespace LLMR.Services
{
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
                throw new ArgumentNullException(nameof(pythonPath), "Python path cannot be null or empty.");

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

        private void ExtractEmbeddedScripts(string targetDirectory)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames();

            foreach (var resourceName in resourceNames)
            {
                if (!resourceName.EndsWith(".py")) continue;
                var fileName = Path.GetFileName(resourceName);
                var filePath = Path.Combine(targetDirectory, fileName);

                ConsoleMessageOccurred?.Invoke(this, $"Extracting script: {fileName} to {filePath}");

                if (!File.Exists(filePath))
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream == null)
                    {
                        ConsoleMessageOccurred?.Invoke(this, $"Resource stream for {resourceName} is null.");
                        continue;
                    }

                    using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                    stream.CopyTo(fileStream);
                    ConsoleMessageOccurred?.Invoke(this, $"Script {fileName} extracted successfully.");
                }
                else
                {
                    ConsoleMessageOccurred?.Invoke(this, $"Script {fileName} already exists at {filePath}.");
                }
            }
        }

        private void PythonThreadStart()
        {
            try
            {
                var baseDataDir = AppDataPath.GetBaseDataDirectory();
                ConsoleMessageOccurred?.Invoke(this, $"Base data directory: {baseDataDir}");
                ExtractEmbeddedScripts(baseDataDir);

                if (!Directory.Exists(_pythonPath))
                    throw new DirectoryNotFoundException($"<PES.cs error> Invalid Python path: {_pythonPath}");

                var pythonExecutable = Path.Combine(_pythonPath, "bin", "python3.12");
                ConsoleMessageOccurred?.Invoke(this, $"Python Executable Path: {pythonExecutable}");

                if (!File.Exists(pythonExecutable))
                    throw new FileNotFoundException($"<PES.cs error> Python executable not found at {pythonExecutable}");

                var (pythonVersion, pythonDllPath) = GetPythonVersionAndDllPath(pythonExecutable);

                ConsoleMessageOccurred?.Invoke(this, $"Detected Python version: {pythonVersion}");
                ConsoleMessageOccurred?.Invoke(this, $"Python DLL Path: {pythonDllPath}");

                if (!File.Exists(pythonDllPath))
                    throw new FileNotFoundException($"<PES.cs error> Python dynamic library not found at {pythonDllPath}");

                Runtime.PythonDLL = pythonDllPath;
                PythonEngine.Initialize();
                ConsoleMessageOccurred?.Invoke(this, "PythonEngine initialized successfully.");
                _isPythonInitialized = true;
                _initTcs.TrySetResult(true);

                using (Py.GIL())
                {
                    dynamic sys = Py.Import("sys");
                    var scriptsPath = Path.Combine(baseDataDir, "Python", "312", "lib", $"python{pythonVersion}");
                    var sitePackagesPath = Path.Combine(scriptsPath, "site-packages");
                    var scriptsDir = Path.Combine(baseDataDir, "Scripts");

                    ConsoleMessageOccurred?.Invoke(this, $"Appending to sys.path: {scriptsPath}");
                    sys.path.append(scriptsPath);
                    ConsoleMessageOccurred?.Invoke(this, $"Appending to sys.path: {sitePackagesPath}");
                    sys.path.append(sitePackagesPath);
                    ConsoleMessageOccurred?.Invoke(this, $"Appending to sys.path: {scriptsDir}");
                    sys.path.append(scriptsDir);

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
                    Func<Task>? taskFunc = _taskQueue.Take();
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
                dynamic importlibMetadata = Py.Import("importlib.metadata");
                string[] requiredPackages = { "requests", "openai", "gradio" };
                string[] requiredVersions = { "2.31", "1.54", "5.1" };

                for (var i = 0; i < requiredPackages.Length; i++)
                {
                    var package = requiredPackages[i];
                    var requiredVersion = requiredVersions[i];
                    try
                    {
                        string installedVersion = importlibMetadata.version(package);
                        ConsoleMessageOccurred?.Invoke(this, $"Checking package '{package}'. Installed version: {installedVersion}, Required version: {requiredVersion}");

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

            var versionInfo = lines[0].Trim('(', ')').Split(',');
            var major = versionInfo[0].Trim();
            var minor = versionInfo[1].Trim();
            var version = $"{major}.{minor}";

            var libDir = lines[1].Trim();
            var pythonDllName = $"libpython{version}.dylib";
            var pythonDllPath = Path.Combine(libDir, pythonDllName);

            ConsoleMessageOccurred?.Invoke(this, $"LIBDIR: {libDir}");
            ConsoleMessageOccurred?.Invoke(this, $"Python DLL Name: {pythonDllName}");
            ConsoleMessageOccurred?.Invoke(this, $"Python DLL Path: {pythonDllPath}");

            if (!File.Exists(pythonDllPath))
                throw new FileNotFoundException($"<PES.cs error> Python dynamic library not found at '{pythonDllPath}'.");

            return (version, pythonDllPath);
        }

        public static void PythonStdout(string message)
        {
            _instance?.ConsoleMessageOccurred?.Invoke(_instance, $"<PES stdout> {message}");
        }

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
}
