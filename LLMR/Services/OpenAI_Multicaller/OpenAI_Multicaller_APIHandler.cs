using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LLMR.Model.ModelSettingModulesManager;
using LLMR.Model.ModelSettingModulesManager.ModelParameters;
using LLMR.Model.ModelSettingModulesManager.ModelSettingsModules;
using Python.Runtime;

namespace LLMR.Services.OpenAI_Multicaller;

    public class OpenAI_Multicaller_APIHandler : IAPIHandler, IDisposable
    {
        private readonly PythonExecutionService? _pythonService;
        
        public OpenAI_Multicaller_APIHandler(PythonExecutionService? pythonService)
        {
            _pythonService = pythonService;
            PythonPath = pythonService.GetPythonPath() ??
                         throw new ArgumentNullException(nameof(pythonService.GetPythonPath));
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

        public Task<(string LocalUrl, string PublicUrl)> StartGradioInterfaceAsync(string apiKey, IModelSettings? settings)
        {
            throw new NotImplementedException("<OAI MulticallerAPIH: no gradio interface in multicaller mode.>");
        }

        public Task<string> StopGradioInterfaceAsync()
        {
            throw new NotImplementedException("<OAI MulticallerAPIH: no gradio interface in multicaller mode.>");
        }

        public async Task<string> RunMulticallerAsync(string apiKey, IModelSettings? settings)
        {
            if (settings is not OpenAI_Multicaller_ModelSettings)
            {
                throw new ArgumentException("Settings must be of type OpenAI_Multicaller_ModelSettings!");
            }

            return await Task.Run(() =>
            {
                var prompt = settings.Parameters
                    .OfType<StringParameter>()
                    .FirstOrDefault(p => p.Name == "Prompt (User):")?.ValueTyped ?? "";

                var n = settings.Parameters
                    .OfType<IntParameter>()
                    .FirstOrDefault(p => p.Name == "n")?.ValueTyped ?? 2;

                var systemMessage = settings.Parameters
                    .OfType<StringParameter>()
                    .FirstOrDefault(p => p.Name == "System message")?.ValueTyped ?? "You are a helpful assistant.";

                var temperature = settings.Parameters
                    .OfType<DoubleParameter>()
                    .FirstOrDefault(p => p.Name == "Temperature")?.ValueTyped ?? 1.0;

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
                
                var argumentsBuilder = new StringBuilder();

                // PATH handling happening here!
                var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "openAI_multicaller.py");
                OnConsoleMessageOccured($"<OAI_MC APIH> scriptPath is set to: {scriptPath}");
                scriptPath = scriptPath.Replace("\\", "/"); // correct path format across the os'es
                OnConsoleMessageOccured($"<OAI_MC APIH> scriptPath was reformatted into: {scriptPath}");
                argumentsBuilder.Append($"-u \"{scriptPath}\"");
                argumentsBuilder.Append($" --api_key \"{apiKey}\"");
                argumentsBuilder.Append($" --prompt \"{prompt}\"");
                argumentsBuilder.Append($" --n {n}");
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
                    pythonExecutable = Path.Combine(Path.GetDirectoryName(PythonPath), "bin", "python3.12");
                }
                else 
                {
                    throw new NotImplementedException("<OAI_MC APIH> calling from not implemented RuntimeInformation.OSPlatform.");
                }
                
                OnConsoleMessageOccured($"<OAI_MC APIH> pythonExecutable is set to: {pythonExecutable}");


                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonExecutable,
                    // note from moe: deployment... :D
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        outputBuilder.AppendLine(args.Data);
                        OnConsoleMessageOccured(args.Data);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        errorBuilder.AppendLine(args.Data);
                        OnErrorMessageOccured(args.Data);
                    }
                };

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        return "Multicaller script completed successfully.";
                    }
                    else
                    {
                        return $"Multicaller script exited with code {process.ExitCode}.";
                    }
                }
                catch (Exception ex)
                {
                    OnErrorMessageOccured(ex.Message);
                    return $"Error while running multicaller script: {ex.Message}";
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
        }
    }
