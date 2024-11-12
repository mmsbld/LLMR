using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;

namespace LLMR.Services;

public class PythonEnvironmentManager : ReactiveObject
{
    private readonly string _pythonInstallDir;
    private bool IsPythonEnvironmentReady { get; set; }

    public event Action<string, SolidColorBrush>? ConsoleMessageOccurred;
    public event Action<Exception>? ExceptionOccurred;

    public PythonEnvironmentManager()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LLMR");
            _pythonInstallDir = Path.Combine(appDataDir, "Python", "312");
            LogPathUsage(_pythonInstallDir);
        }
        else
        {
            var baseDir = AppContext.BaseDirectory;
            _pythonInstallDir = Path.Combine(baseDir, "Scripts", "Python", "312");
            LogPathUsage(_pythonInstallDir);
        }
    }

    public async Task EnsurePythonEnvironmentAsync()
    {
        try
        {
            IsPythonEnvironmentReady = await CheckPythonEnvironmentAsync();

            if (!IsPythonEnvironmentReady)
            {
                await InstallPythonEnvironmentAsync();
                IsPythonEnvironmentReady = await CheckPythonEnvironmentAsync();
            }

            if (IsPythonEnvironmentReady)
            {
                ConsoleMessageOccurred?.Invoke("<PEM> Python environment is ready.", new SolidColorBrush(Colors.MediumSpringGreen));
            }
            else
            {
                ConsoleMessageOccurred?.Invoke("<PEM> Failed to set up Python environment.", new SolidColorBrush(Colors.MediumVioletRed));
            }
        }
        catch (Exception ex)
        {
            ExceptionOccurred?.Invoke(ex);
        }
    }

    private async Task<bool> CheckPythonEnvironmentAsync()
    {
        try
        {
            var pythonExecutable = GetPythonExecutablePath();
            LogPathUsage(pythonExecutable);

            if (!File.Exists(pythonExecutable))
                return false;

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = "-c \"import sys; import openai; import gradio; print(sys.version.split()[0]); print(openai.__version__); print(gradio.__version__)\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = startInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, args) => outputBuilder.AppendLine(args.Data);
            process.ErrorDataReceived += (sender, args) => errorBuilder.AppendLine(args.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                return false;

            var output = outputBuilder.ToString();
            var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 3)
                return false;

            if (!lines[0].StartsWith("3.12"))
                return false;

            return lines[1].StartsWith("1.54") && lines[2].StartsWith("5.1");
        }
        catch
        {
            return false;
        }
    }

    private async Task InstallPythonEnvironmentAsync()
{
    try
    {
        PostConsoleMessage("<PEM> Installing Python environment...", Colors.MediumSlateBlue);

        string scriptPath;
        string arguments;
        ProcessStartInfo startInfo;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "install_python_windows.ps1");
            arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" \"{_pythonInstallDir}\"";

            LogPathUsage(scriptPath);

            startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "install_python_mac.sh");
            arguments = $"\"{_pythonInstallDir}\"";

            LogPathUsage(scriptPath);

            startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"{scriptPath} {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            throw new PlatformNotSupportedException("<PEM> Unsupported OS!");
        }

        var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
                PostConsoleMessage($"<PEM> {args.Data}", Colors.MediumSpringGreen);
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
                PostConsoleMessage($"<PEM> {args.Data}", Colors.PaleVioletRed);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var errorMessage = $"<PEM> Python installation failed with exit code {process.ExitCode}.";
            PostConsoleMessage(errorMessage, Colors.PaleVioletRed);
            throw new Exception(errorMessage);
        }

        PostConsoleMessage("<PEM> Python environment installed successfully.", Colors.MediumSpringGreen);
    }
    catch (Exception ex)
    {
        PostConsoleMessage($"<PEM> Exception occurred: {ex.Message}", Colors.PaleVioletRed);
        ExceptionOccurred?.Invoke(ex);
        throw;
    }
}

    public string GetPythonExecutablePath()
    {
        var executableName = GetPythonExecutableName();
        string path;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            path = _pythonInstallDir.EndsWith(@"envs\myenv", StringComparison.OrdinalIgnoreCase) ? Path.Combine(_pythonInstallDir, executableName) : Path.Combine(_pythonInstallDir, "envs", "myenv", executableName);
        }
        else
        {
            path = Path.Combine(_pythonInstallDir, "envs", "myenv", "bin", executableName);
        }

        LogPathUsage(path);
        return path;
    }

    public string GetPythonLibraryPath()
    {
        string path;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            path = _pythonInstallDir.EndsWith(@"envs\myenv", StringComparison.OrdinalIgnoreCase) ? _pythonInstallDir : Path.Combine(_pythonInstallDir, "envs", "myenv");
        }
        else
        {
            path = Path.Combine(_pythonInstallDir, "envs", "myenv", "lib");
        }

        LogPathUsage(path);
        return path;
    }

    private string GetPythonExecutableName()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python.exe" : "python3.12";
    }

    private void LogPathUsage(string path)
    {
        ConsoleMessageOccurred?.Invoke($"<PEM PATH> Path: {path}", new SolidColorBrush(Color.Parse("#A52A2A"))); 
    }

    private void PostConsoleMessage(string message, Color color)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ConsoleMessageOccurred?.Invoke(message, new SolidColorBrush(color));
        });
    }
}