using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Python.Runtime;
using System.Runtime.InteropServices;
using System.IO;

namespace LLMR.Services;

public class PythonExecutionService : IDisposable
{
    private readonly Thread _pythonThread;
    private readonly BlockingCollection<Func<Task>?> _taskQueue = new BlockingCollection<Func<Task>?>();
    private bool _isDisposed = false;
    private bool _isPythonInitialized = false;
    private readonly string _pythonPath;

    public PythonExecutionService(string pythonPath)
    {
        ArgumentNullException.ThrowIfNull(pythonPath);

        _pythonPath = pythonPath;

        _pythonThread = new Thread(PythonThreadStart)
        {
            IsBackground = true
        };
        _pythonThread.Start();
    }

    private void PythonThreadStart()
    {
        try
        {
            // Set PYTHONHOME
            Environment.SetEnvironmentVariable("PYTHONHOME", _pythonPath);

            // Get the Python version from the path
            string pythonVersion = new DirectoryInfo(_pythonPath).Name;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // For Windows, set PATH to include pythonPath
                string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                string newPath = $"{_pythonPath};{path}";
                Environment.SetEnvironmentVariable("PATH", newPath);

                // Set PythonDLL
                string dllName = $"python{pythonVersion.Replace(".", "")}.dll";
                Runtime.PythonDLL = Path.Combine(_pythonPath, dllName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // For Linux
                Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", Path.Combine(_pythonPath, "lib"));

                // Set PythonDLL
                string dllName = $"libpython{pythonVersion}.so";
                Runtime.PythonDLL = Path.Combine(_pythonPath, "lib", dllName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // For macOS
                Environment.SetEnvironmentVariable("DYLD_LIBRARY_PATH", Path.Combine(_pythonPath, "lib"));

                // Set PythonDLL
                string dllName = $"libpython{pythonVersion}.dylib";
                Runtime.PythonDLL = Path.Combine(_pythonPath, "lib", dllName);
            }
            else
            {
                throw new PlatformNotSupportedException("<PES.cs (!) error: > Unsupported operating system.");
            }

            PythonEngine.Initialize();
            _isPythonInitialized = true;

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
        finally
        {
            if (_isPythonInitialized)
            {
                try
                {
                    PythonEngine.Shutdown();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"((( internal console PES.cs ))): Error while shutting down python interpreter: {ex.Message}");
                }
            }
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
                    T result = func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }

            return Task.CompletedTask;
        });

        return tcs.Task;
    }

    public void Dispose()
    {
        _isDisposed = true;
        _taskQueue.CompleteAdding();
        _pythonThread.Join(); // wait until thread is finished
    }
}
