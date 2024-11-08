using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;

namespace LLMR.Services
{
    public class PythonEnvironmentManager : ReactiveObject
    {
        private readonly string _pythonInstallDir;
        public bool IsPythonEnvironmentReady { get; private set; }

        public event Action<string, SolidColorBrush>? ConsoleMessageOccurred;
        public event Action<Exception>? ExceptionOccurred;

        public PythonEnvironmentManager()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _pythonInstallDir = Path.Combine(baseDir, "Scripts", "Python", "312");
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
                Dispatcher.UIThread.Post(() =>
                {
                    ConsoleMessageOccurred?.Invoke("<PEM> Installing Python environment...", new SolidColorBrush(Colors.MediumSlateBlue));
                });

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    await InstallPythonOnWindowsAsync();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "install_python_mac.sh");
                    string arguments = $"\"{_pythonInstallDir}\"";

                    await RunShellScriptAsync("/bin/bash", scriptPath, arguments);
                }
                else
                {
                    throw new PlatformNotSupportedException("<PEM> Unsupported OS!");
                }

                Dispatcher.UIThread.Post(() =>
                {
                    ConsoleMessageOccurred?.Invoke("<PEM> Python environment installed successfully.", new SolidColorBrush(Colors.MediumSpringGreen));
                });
            }
            catch (Exception ex)
            {
                ExceptionOccurred?.Invoke(ex);
                throw;
            }
        }

        private async Task InstallPythonOnWindowsAsync()
        {
            string pythonVersion = "3.12.0";
            string pythonInstallerUrl = $"https://www.python.org/ftp/python/{pythonVersion}/python-{pythonVersion}-amd64.exe";
            string installerPath = Path.Combine(Path.GetTempPath(), $"python-{pythonVersion}-amd64.exe");

            try
            {
                // Download Python installer
                ConsoleMessageOccurred?.Invoke("<PEM> Downloading Python installer...", new SolidColorBrush(Colors.MediumSlateBlue));
                using (var client = new WebClient())
                {
                    await client.DownloadFileTaskAsync(new Uri(pythonInstallerUrl), installerPath);
                }

                // Install Python
                ConsoleMessageOccurred?.Invoke("<PEM> Installing Python...", new SolidColorBrush(Colors.MediumSlateBlue));
                var installInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = $"/quiet InstallAllUsers=0 PrependPath=0 TargetDir=\"{_pythonInstallDir}\" Include_pip=1",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var process = new Process { StartInfo = installInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, args) => outputBuilder.AppendLine(args.Data);
                process.ErrorDataReceived += (sender, args) => errorBuilder.AppendLine(args.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"<PEM> Python installation failed. Exit code: {process.ExitCode}\nError: {errorBuilder}");
                }

                // Update PATH environment variable
                ConsoleMessageOccurred?.Invoke("<PEM> Updating PATH environment variable...", new SolidColorBrush(Colors.MediumSlateBlue));
                string currentPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine);
                if (!currentPath.Contains(_pythonInstallDir))
                {
                    string newPath = $"{currentPath};{_pythonInstallDir}";
                    Process setxProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "setx",
                        Arguments = $"PATH \"{newPath}\" /M",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    setxProcess.WaitForExit();
                }

                // Install required packages
                ConsoleMessageOccurred?.Invoke("<PEM> Installing required Python packages...", new SolidColorBrush(Colors.MediumSlateBlue));
                string pythonExePath = GetPythonExecutablePath();
                await InstallPythonPackagesAsync(pythonExePath);
            }
            finally
            {
                // Clean up installer
                if (File.Exists(installerPath))
                {
                    File.Delete(installerPath);
                }
            }
        }

        private async Task InstallPythonPackagesAsync(string pythonExePath)
        {
            string[] packages = new[]
            {
                "-m pip install --upgrade pip setuptools",
                "-m pip install openai==1.54 requests==2.31 gradio==5.1.0"
            };

            foreach (var package in packages)
            {
                var pipInfo = new ProcessStartInfo
                {
                    FileName = pythonExePath,
                    Arguments = package,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var process = new Process { StartInfo = pipInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, args) => outputBuilder.AppendLine(args.Data);
                process.ErrorDataReceived += (sender, args) => errorBuilder.AppendLine(args.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"<PEM> Failed to install Python package: {package}\nError: {errorBuilder}");
                }

                Dispatcher.UIThread.Post(() =>
                {
                    ConsoleMessageOccurred?.Invoke($"<PEM> {package} installed successfully.", new SolidColorBrush(Colors.MediumSpringGreen));
                });
            }
        }

        private async Task RunShellScriptAsync(string shell, string scriptPath, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = $"\"{scriptPath}\" {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    Dispatcher.UIThread.Post(() =>
                    {
                        ConsoleMessageOccurred?.Invoke($"<PEM> {args.Data}", new SolidColorBrush(Colors.MediumSpringGreen));
                    });
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    Dispatcher.UIThread.Post(() =>
                    {
                        ConsoleMessageOccurred?.Invoke($"<PEM> {args.Data}", new SolidColorBrush(Colors.PaleVioletRed));
                    });
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception("<PEM> Shell script execution failed.");
        }

        public string GetPythonExecutablePath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(_pythonInstallDir, "python.exe");
            }
            else
            {
                return Path.Combine(_pythonInstallDir, "bin", "python3.12");
            }
        }

        public string GetPythonLibraryPath()
        {
            return _pythonInstallDir;
        }
    }
}
