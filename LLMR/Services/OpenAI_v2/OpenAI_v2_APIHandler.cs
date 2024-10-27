using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LLMR.Models.ModelSettingsManager;
using LLMR.Models.ModelSettingsManager.ModelParameters;
using LLMR.Models.ModelSettingsManager.ModelSettingsModules;
using Python.Runtime;

namespace LLMR.Services.OpenAI_v2;

    public class OpenAI_v2_APIHandler : IAPIHandler, IDisposable
    {
        private readonly PythonExecutionService _pythonService;
        private Process? _gradioProcess;

        private string _localUrl;
        private string _publicUrl;

        private TaskCompletionSource<(string LocalUrl, string PublicUrl)> _gradioUrlsTcs;

        public OpenAI_v2_APIHandler(PythonExecutionService pythonService, string? pythonPath)
        {
            _pythonService = pythonService;
            PythonPath = pythonPath ?? throw new ArgumentNullException(nameof(pythonPath));
            _localUrl = "<internal APIH: empty string>";
            _publicUrl = "<internal APIH: empty string>";
        }

        public event EventHandler<string> ConsoleMessageOccured;
        public event EventHandler<string> ErrorMessageOccured;

        public string? PythonPath { get; }

        public Task<bool> ValidateApiKeyAsync(string apiKey)
        {
            return _pythonService.ExecuteAsync<bool>(() =>
            {
                dynamic sys = Py.Import("sys");
                sys.path.append("Scripts");

                dynamic apiModule = Py.Import("openAI_v2_apiHandler");

                dynamic result = apiModule.validate_api_key(apiKey);

                return (bool)result;
            });
        }

        public Task<List<string>> GetAvailableModelsAsync(string apiKey)
        {
            return _pythonService.ExecuteAsync<List<string>>(() =>
            {
                dynamic sys = Py.Import("sys");
                sys.path.append("Scripts");

                dynamic apiModule = Py.Import("openAI_v2_apiHandler");

                dynamic result = apiModule.get_available_models(apiKey);

                List<string> models = new List<string>();
                foreach (dynamic model in result)
                {
                    models.Add((string)model);
                }

                return models;
            });
        }

        public async Task<(string LocalUrl, string PublicUrl)> StartGradioInterfaceAsync(string apiKey, IModelSettings settings)
        {
            _gradioUrlsTcs = new TaskCompletionSource<(string, string)>();
            
            if (!(settings is OpenAI_v2_ModelSettings))
            {
                throw new ArgumentException("<APIH error> Settings must be of type OpenAI_v2_ModelSettings!");
            }

            return await Task.Run(async () =>
            {
                if (_gradioProcess != null && !_gradioProcess.HasExited)
                {
                    return (_localUrl, _publicUrl);
                }

                // Extract parameters
                var systemMessage = settings.Parameters
                    .OfType<StringParameter>()
                    .FirstOrDefault(p => p.Name == "System message")?.ValueTyped ?? "You are a helpful assistant.";

                var temperature = settings.Parameters
                    .OfType<DoubleParameter>()
                    .FirstOrDefault(p => p.Name == "Temperature")?.ValueTyped ?? 0.7;

                var topP = settings.Parameters
                    .OfType<DoubleParameter>()
                    .FirstOrDefault(p => p.Name == "TopP")?.ValueTyped ?? 1.0;

                var maxTokens = settings.Parameters
                    .OfType<IntParameter>()
                    .FirstOrDefault(p => p.Name == "MaxTokens")?.ValueTyped;

                var frequencyPenalty = settings.Parameters
                    .OfType<DoubleParameter>()
                    .FirstOrDefault(p => p.Name == "FrequencyPenalty")?.ValueTyped ?? 0.0;

                var presencePenalty = settings.Parameters
                    .OfType<DoubleParameter>()
                    .FirstOrDefault(p => p.Name == "PresencePenalty")?.ValueTyped ?? 0.0;

                // Build arguments string
                var argumentsBuilder = new StringBuilder();

                argumentsBuilder.Append($"-u Scripts/openAI_v2_gradioServer.py --start-gradio");
                argumentsBuilder.Append($" --api_key \"{apiKey}\"");
                argumentsBuilder.Append($" --model \"{settings.SelectedModel}\"");
                argumentsBuilder.Append($" --system_message \"{systemMessage}\"");
                argumentsBuilder.Append($" --temperature {temperature.ToString(CultureInfo.InvariantCulture)}");
                argumentsBuilder.Append($" --top_p {topP.ToString(CultureInfo.InvariantCulture)}");
                argumentsBuilder.Append($" --frequency_penalty {frequencyPenalty.ToString(CultureInfo.InvariantCulture)}");
                argumentsBuilder.Append($" --presence_penalty {presencePenalty.ToString(CultureInfo.InvariantCulture)}");

                if (maxTokens.HasValue)
                {
                    argumentsBuilder.Append($" --max_tokens {maxTokens.Value}");
                }

                var arguments = argumentsBuilder.ToString();

                string pythonExecutable;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    pythonExecutable = Path.Combine(PythonPath, "python.exe");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    pythonExecutable = Path.Combine(PythonPath, "bin", "python3");
                }
                else 
                {
                    throw new NotImplementedException("<OAI Multicaller APIHandler:> calling from not implemented RuntimeInformation.OSPlatform.");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonExecutable, // note from moe: deployment could be a pain!)
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _gradioProcess = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                _gradioProcess.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        // handle output data (gradio -u)
                        if (args.Data.Contains("Running on local URL"))
                        {
                            _localUrl = args.Data.Split("Running on local URL: ")[1].Trim();
                        }
                        else if (args.Data.Contains("Running on public URL"))
                        {
                            _publicUrl = args.Data.Split("Running on public URL: ")[1].Trim();
                            _gradioUrlsTcs.TrySetResult((_localUrl, _publicUrl));
                        }
                        else if (args.Data.Contains("<GSPY internal>"))
                        {
                            OnConsoleMessageOccured(args.Data.Split("<GSPY internal>")[1].Trim());
                        }
                    }
                };

                _gradioProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        OnErrorMessageOccured("<internal error APIH 154> " + args.Data);
                    }
                };

                try
                {
                    _gradioProcess.Start();
                    _gradioProcess.BeginOutputReadLine();
                    _gradioProcess.BeginErrorReadLine();

                    var completedTask = await Task.WhenAny(_gradioUrlsTcs.Task, Task.Delay(20000)); // Wait up to 20 seconds

                    if (completedTask == _gradioUrlsTcs.Task)
                    {
                        return await _gradioUrlsTcs.Task;
                    }
                    else
                    {
                        // if the task didn't complete -> default error
                        _publicUrl = "<internal error APIH 42>";
                        return (_localUrl, _publicUrl);
                    }
                }
                catch (Exception ex)
                {
                    return ("<internal error APIH 245>", "<internal error APIH 245>");
                }
            });
        }

        public Task<string> StopGradioInterfaceAsync()
        {
            return Task.Run(() =>
            {
                if (_gradioProcess == null || _gradioProcess.HasExited)
                {
                    return "Gradio interface not running.";
                }

                try
                {
                    _gradioProcess.Kill();
                    _gradioProcess.WaitForExit();
                    _gradioProcess = null;
                    return "Gradio interface stopped.";
                }
                catch (Exception ex)
                {
                    return $"Error while stopping: {ex.Message}";
                }
            });
        }

        protected virtual void OnConsoleMessageOccured(string message)
        {
            ConsoleMessageOccured?.Invoke(this, message);
        }

        protected virtual void OnErrorMessageOccured(string message)
        {
            ErrorMessageOccured?.Invoke(this, message);
        }

        public void Dispose()
        {
            _pythonService.Dispose();

            if (_gradioProcess != null && !_gradioProcess.HasExited)
            {
                _gradioProcess.Kill();
            }
        }
    }