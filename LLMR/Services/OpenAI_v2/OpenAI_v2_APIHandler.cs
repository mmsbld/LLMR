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
// ReSharper disable InconsistentNaming

namespace LLMR.Services.OpenAI_v2;

public sealed class OpenAI_v2_APIHandler(PythonExecutionService? pythonService)
    : IAPIHandler, IDisposable
{
    private Process? _gradioProcess;

    private string _localUrl = "<internal APIH: empty string>";
    private string _publicUrl = "<internal APIH: empty string>";

    private TaskCompletionSource<(string LocalUrl, string PublicUrl)>? _gradioUrlsTcs;

    public event EventHandler<string>? ConsoleMessageOccured;
    public event EventHandler<string>? ErrorMessageOccured;

    private string? PythonPath { get; } = pythonService.GetPythonPath() ?? throw new ArgumentNullException(nameof(pythonService.GetPythonPath));

    public Task<bool> ValidateApiKeyAsync(string apiKey)
    {
        if (pythonService == null)
            throw new NullReferenceException("Python service is null.");
                
        return pythonService.ExecuteAsync<bool>(() =>
        {
            dynamic sys = Py.Import("sys");
            sys.path.append("Scripts");

            dynamic apiModule = Py.Import("openAI_v2_apiHandler");

            var result = apiModule.validate_api_key(apiKey);

            if (result == null)
            {
                throw new InvalidOperationException("<APIH oAIv2> Validation result is null, check Python API.");
            }

            return (bool)result;
        });
    }

    public Task<List<string>> GetAvailableModelsAsync(string apiKey)
    {
        if (pythonService == null)
            throw new NullReferenceException("Python service is null.");
                
        return pythonService.ExecuteAsync(() =>
        {
            dynamic sys = Py.Import("sys");
            sys.path.append("Scripts");

            dynamic apiModule = Py.Import("openAI_v2_apiHandler");

            var result = apiModule.get_available_models(apiKey);

            var models = new List<string>();
            foreach (var model in result)
            {
                models.Add((string)model);
            }

            return models;
        });
    }

    public async Task<(string LocalUrl, string PublicUrl)> StartGradioInterfaceAsync(string apiKey, IModelSettings? settings)
    {
        _gradioUrlsTcs = new TaskCompletionSource<(string, string)>();
                
        if (!(settings is OpenAI_v2_ModelSettings))
        {
            throw new ArgumentException("<APIH oAIv2> Settings must be of type OpenAI_v2_ModelSettings!");
        }

        return await Task.Run(async () =>
        {
            if (_gradioProcess is { HasExited: false })
            {
                return (_localUrl, _publicUrl);
            }

            // extract parameters
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
            
            var argumentsBuilder = new StringBuilder();

            // PATH handling happening here!
            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "openAI_v2_gradioServer.py");
            OnConsoleMessageOccured($"<OAI_v2 APIH> scriptPath is set to: {scriptPath}");
            scriptPath = scriptPath.Replace("\\", "/"); //  correct path format (win/mac/... !)
            OnConsoleMessageOccured($"<OAI_v2 APIH> scriptPath was reformatted into: {scriptPath}");

            argumentsBuilder.Append($"-u \"{scriptPath}\" --start-gradio");
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

            if (PythonPath is null)
                throw new NullReferenceException("PythonPath is null.");
                    
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
                throw new Exception("<APIH oAIv2> Calling from not implemented RuntimeInformation.OSPlatform.");
            }
            
            OnConsoleMessageOccured($"<OAI_v2 APIH> pythonExecutable is set to: {pythonExecutable}");

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable, 
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                // WorkingDirectory = AppContext.BaseDirectory
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
                    // Handle output data
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
                    else
                    {
                        OnConsoleMessageOccured(args.Data);
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

                var completedTask = await Task.WhenAny(_gradioUrlsTcs.Task, Task.Delay(20000)); // wait 20s max.

                if (completedTask == _gradioUrlsTcs.Task)
                {
                    return await _gradioUrlsTcs.Task;
                }
                else
                {
                    // Time over -> default error as link
                    _publicUrl = "<internal error APIH 42>";
                    return (_localUrl, _publicUrl);
                }
            }
            catch (Exception ex)
            {
                OnErrorMessageOccured(ex.Message);
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
                return "<APIH oAIv2> Gradio interface not running.";
            }

            try
            {
                _gradioProcess.Kill();
                _gradioProcess.WaitForExit();
                _gradioProcess = null;
                return "<APIH oAIv2> Gradio interface stopped successfully.";
            }
            catch (Exception ex)
            {
                return $"Error while stopping: {ex.Message}";
            }
        });
    }

    public Task<string> RunMulticallerAsync(string apiKey, IModelSettings? settings)
    {
        throw new NotImplementedException();
    }

    private void OnConsoleMessageOccured(string message)
    {
        ConsoleMessageOccured?.Invoke(this, message);
    }

    private void OnErrorMessageOccured(string message)
    {
        ErrorMessageOccured?.Invoke(this, message);
    }

    public void Dispose()
    {
        if (pythonService == null)
            throw new NullReferenceException("_pythonService is null.");
        pythonService.Dispose();

        if (_gradioProcess is { HasExited: false })
        {
            _gradioProcess.Kill();
        }
    }
}
