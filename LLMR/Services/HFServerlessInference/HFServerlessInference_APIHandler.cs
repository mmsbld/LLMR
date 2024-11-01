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

namespace LLMR.Services.HFServerlessInference;

public class HFServerlessInference_APIHandler : IAPIHandler, IDisposable
{
    private readonly PythonExecutionService? _pythonService;
    private Process _gradioProcess;

    private string _localUrl;
    private string _publicUrl;

    private TaskCompletionSource<(string LocalUrl, string PublicUrl)> _gradioUrlsTcs = new TaskCompletionSource<(string, string)>();

    public HFServerlessInference_APIHandler(PythonExecutionService? pythonService, string? pythonPath)
    {
        _pythonService = pythonService;
        PythonPath = pythonPath ?? throw new ArgumentNullException(nameof(pythonPath));
        _localUrl = "<internal APIH: empty string>";
        _publicUrl = "<internal APIH: empty string>";
    }

    public event EventHandler<string> ConsoleMessageOccured;
    public event EventHandler<string> ErrorMessageOccured;

    public string? PythonPath { get; }

    public Task<bool> ValidateApiKeyAsync(string apiToken)
    {
        return _pythonService.ExecuteAsync<bool>(() =>
        {
            dynamic sys = Py.Import("sys");
            sys.path.append("Scripts");

            dynamic apiModule = Py.Import("hfServerlessInference_apiHandler");

            dynamic result = apiModule.validate_api_token(apiToken);
            // note from moe: correct functioning true) with status_code 200 in response from HF

            return (bool)result;
        });
    }

    public Task<List<string>> GetAvailableModelsAsync(string apiToken)
    {
        return _pythonService.ExecuteAsync<List<string>>(() =>
        {
            dynamic sys = Py.Import("sys");
            sys.path.append("Scripts");

            dynamic apiModule = Py.Import("hfServerlessInference_apiHandler");

            dynamic result = apiModule.get_available_models(apiToken);

            List<string> models = new List<string>();
            foreach (dynamic model in result)
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

            argumentsBuilder.Append($"-u Scripts/hfServerlessInference_gradioServer.py --start-gradio");
            argumentsBuilder.Append($" --api_token \"{apiToken}\"");
            argumentsBuilder.Append($" --model_id \"{settings.SelectedModel}\"");
            argumentsBuilder.Append($" --system_message \"{systemMessage}\"");
            argumentsBuilder.Append($" --temperature {temperature.ToString(CultureInfo.InvariantCulture)}");

            if (topP.HasValue)
            {
                double topPValue = topP.Value;
                argumentsBuilder.Append($" --top_p {topPValue.ToString(CultureInfo.InvariantCulture)}");
            }

            if (frequencyPenalty.HasValue)
            {
                double frequencyPenaltyValue = frequencyPenalty.Value;
                argumentsBuilder.Append($" --frequency_penalty {frequencyPenaltyValue.ToString(CultureInfo.InvariantCulture)}");
            }

            if (presencePenalty.HasValue)
            {
                double presencePenaltyValue = presencePenalty.Value;
                argumentsBuilder.Append($" --presence_penalty {presencePenaltyValue.ToString(CultureInfo.InvariantCulture)}");
            }
                
            argumentsBuilder.Append($" --stop_sequences \"{stopSequences.Replace("\"", "\\\"")}\"");
                
            if (maxCompletionTokens.HasValue)
            {
                argumentsBuilder.Append($" --max_completion_tokens {maxCompletionTokens.Value}");
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
            catch (Exception)
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