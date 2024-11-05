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
    private readonly string _pythonPath;
    public bool IsPythonEnvironmentReady { get; private set; }

    public event Action<string, SolidColorBrush>? ConsoleMessageOccurred;
    public event Action<Exception>? ExceptionOccurred;

    public PythonEnvironmentManager()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _pythonPath = Path.Combine(baseDir, "Scripts", "Python", "312");
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
            string pythonExecutable = GetPythonExecutablePath();

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

            return lines[1].StartsWith("1.54") && lines[2].StartsWith("5.4");
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
            Dispatcher.UIThread.Post(() => {
                ConsoleMessageOccurred?.Invoke("<PEM> Installing Python environment...", new SolidColorBrush(Colors.MediumSlateBlue));
            });
            string scriptPath;
            string arguments;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "install_python_windows.bat");
                arguments = $"\"{_pythonPath}\"";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "install_python_mac.sh");
                arguments = $"\"{_pythonPath}\"";
            }
            else
            {
                throw new PlatformNotSupportedException("<PEM> Unsupported OS!");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/bash",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? $"/c \"{scriptPath}\" {arguments}"
                    : $"{scriptPath} {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (sender, args) => {
                if (args.Data != null)
                    Dispatcher.UIThread.Post(() => {
                        ConsoleMessageOccurred?.Invoke($"<PEM> {args.Data}", new SolidColorBrush(Colors.MediumSpringGreen));
                    });
            };

            process.ErrorDataReceived += (sender, args) => {
                if (args.Data != null)
                    Dispatcher.UIThread.Post(() => {
                        ConsoleMessageOccurred?.Invoke($"<PEM> {args.Data}", new SolidColorBrush(Colors.PaleVioletRed));
                    });
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception("<PEM> Python installation failed.");

            Dispatcher.UIThread.Post(() => {
                ConsoleMessageOccurred?.Invoke("<PEM> Python environment installed successfully.", new SolidColorBrush(Colors.MediumSpringGreen));
            });
        }
        catch (Exception ex)
        {
            ExceptionOccurred?.Invoke(ex);
            throw;
        }
    }

    public string GetPythonExecutablePath()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(_pythonPath, "Scripts", "python.exe")
            : Path.Combine(_pythonPath, "bin", "python3");
    }

    public string GetPythonLibraryPath()
    {
        return _pythonPath;
    }
}