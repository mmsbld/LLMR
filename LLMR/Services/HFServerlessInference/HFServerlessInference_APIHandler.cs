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

namespace LLMR.Services.HFServerlessInference;

public sealed class HFServerlessInference_APIHandler(PythonExecutionService? pythonService) : IAPIHandler, IDisposable
{
    private Process _gradioProcess;

    private string _localUrl = "<internal APIH: empty string>";
    private string _publicUrl = "<internal APIH: empty string>";

    private TaskCompletionSource<(string LocalUrl, string PublicUrl)> _gradioUrlsTcs = new();

    public event EventHandler<string> ConsoleMessageOccured;
    public event EventHandler<string> ErrorMessageOccured;

    public string? PythonPath { get; } = pythonService.GetPythonPath() ?? throw new ArgumentNullException(nameof(pythonService.GetPythonPath));

    public Task<bool> ValidateApiKeyAsync(string apiToken)
    {
        return pythonService.ExecuteAsync<bool>(() =>
        {
            dynamic sys = Py.Import("sys");
            sys.path.append("Scripts");

            dynamic apiModule = Py.Import("hfServerlessInference_apiHandler");

            var result = apiModule.validate_api_token(apiToken);
            // note from moe: correct functioning true) with status_code 200 in response from HF

            return (bool)result;
        });
    }

    public Task<List<string>> GetAvailableModelsAsync(string apiToken)
    {
        return pythonService.ExecuteAsync<List<string>>(() =>
        {
            dynamic sys = Py.Import("sys");
            sys.path.append("Scripts");

            dynamic apiModule = Py.Import("hfServerlessInference_apiHandler");

            var result = apiModule.get_available_models(apiToken);

            List<string> models = new();
            foreach (var model in result)
            {
                models.Add((string)model);
            }

            return models;
        });
    }

    public async Task<(string LocalUrl, string PublicUrl)> StartGradioInterfaceAsync(string apiToken, IModelSettings? settings)
    {
        if (!(settings is HFServerlessInferenceModelSettings))
        {
            throw new ArgumentException("<HFSIMS error> Settings must be of type HFServerlessInferenceModelSettings!");
        }

        return await Task.Run(async () =>
        {
            if (_gradioProcess != null && !_gradioProcess.HasExited)
            {
                return (_localUrl, _publicUrl);
            }

            var systemMessage = settings.Parameters
                .OfType<StringParameter>()
                .FirstOrDefault(p => p.Name == "System message")?.ValueTyped ?? "You are a helpful assistant.";

            var temperature = settings.Parameters
                .OfType<DoubleParameter>()
                .FirstOrDefault(p => p.Name == "Temperature")?.ValueTyped ?? 0.8;

            var topP = settings.Parameters
                .OfType<DoubleParameter>()
                .FirstOrDefault(p => p.Name == "TopP")?.ValueTyped;

            var maxCompletionTokens = settings.Parameters
                .OfType<IntParameter>()
                .FirstOrDefault(p => p.Name == "MaxCompletionTokens")?.ValueTyped;

            var frequencyPenalty = settings.Parameters
                .OfType<DoubleParameter>()
                .FirstOrDefault(p => p.Name == "FrequencyPenalty")?.ValueTyped;

            var presencePenalty = settings.Parameters
                .OfType<DoubleParameter>()
                .FirstOrDefault(p => p.Name == "PresencePenalty")?.ValueTyped;
                
            var stopSequences = settings.Parameters
                .OfType<StringParameter>()
                .FirstOrDefault(p => p.Name == "StopSequences")?.ValueTyped;

            var argumentsBuilder = new StringBuilder();

            // PATH handling happening here!
            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "hfServerlessInference_gradioServer.py");
            OnConsoleMessageOccured($"<APIH HFSI> scriptPath is set to: {scriptPath}");
            scriptPath = scriptPath.Replace("\\", "/"); // correct path format (win/max/...)
            OnConsoleMessageOccured($"<APIH HFSI> scriptPath was reformatted into: {scriptPath}");
            argumentsBuilder.Append($"-u \"{scriptPath}\" --start-gradio");
            argumentsBuilder.Append($" --api_token \"{apiToken}\"");
            argumentsBuilder.Append($" --model_id \"{settings.SelectedModel}\"");
            argumentsBuilder.Append($" --system_message \"{systemMessage}\"");
            argumentsBuilder.Append($" --temperature {temperature.ToString(CultureInfo.InvariantCulture)}");

            if (topP.HasValue)
            {
                var topPValue = topP.Value;
                argumentsBuilder.Append($" --top_p {topPValue.ToString(CultureInfo.InvariantCulture)}");
            }

            if (frequencyPenalty.HasValue)
            {
                var frequencyPenaltyValue = frequencyPenalty.Value;
                argumentsBuilder.Append($" --frequency_penalty {frequencyPenaltyValue.ToString(CultureInfo.InvariantCulture)}");
            }

            if (presencePenalty.HasValue)
            {
                var presencePenaltyValue = presencePenalty.Value;
                argumentsBuilder.Append($" --presence_penalty {presencePenaltyValue.ToString(CultureInfo.InvariantCulture)}");
            }
                
            argumentsBuilder.Append($" --stop_sequences \"{stopSequences.Replace("\"", "\\\"")}\"");
                
            if (maxCompletionTokens.HasValue)
            {
                argumentsBuilder.Append($" --max_completion_tokens {maxCompletionTokens.Value}");
            }

            var arguments = argumentsBuilder.ToString();
            
            if (PythonPath is null)
                throw new NullReferenceException("<APIH HFSI> PythonPath is null.");

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
                throw new Exception("<APIH HFSI> calling from not implemented RuntimeInformation.OSPlatform.");
            }
            OnConsoleMessageOccured($"<APIH HFSI> pythonExecutable is set to: {pythonExecutable}");

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
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

                var completedTask = await Task.WhenAny(_gradioUrlsTcs.Task, Task.Delay(20000)); 

                if (completedTask == _gradioUrlsTcs.Task)
                {
                    return await _gradioUrlsTcs.Task;
                }
                else
                {
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
                return "<APIH HFSI> Gradio interface not running.";
            }

            try
            {
                _gradioProcess.Kill();
                _gradioProcess.WaitForExit();
                _gradioProcess = null;
                return "<APIH HFSI> Gradio interface stopped.";
            }
            catch (Exception ex)
            {
                return $"<APIH HFSI> Error while stopping: {ex.Message}";
            }
        });
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
            throw new NullReferenceException("pythonService is null.");
        pythonService.Dispose();

        if (_gradioProcess is { HasExited: false })
        {
            _gradioProcess.Kill();
        }
    }
}