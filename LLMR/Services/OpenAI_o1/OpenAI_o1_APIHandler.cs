using System;
using System.Collections.Generic;
using System.Diagnostics;
//using System.Globalization; // not used (parameters are not usable as of now, no casting that req. Cult.Info (02/25)) 
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LLMR.Model.ModelSettingModulesManager;
using LLMR.Model.ModelSettingModulesManager.ModelParameters;
using LLMR.Model.ModelSettingModulesManager.ModelSettingsModules;
using Python.Runtime;

namespace LLMR.Services.OpenAI_o1;

public sealed class OpenAI_o1_APIHandler(PythonExecutionService? pythonService) : IAPIHandler, IDisposable
{
    private Process? _gradioProcess;
    private string _localUrl = "<internal APIH: empty string>";
    private string _publicUrl = "<internal APIH: empty string>";
    private TaskCompletionSource<(string LocalUrl, string PublicUrl)>? _gradioUrlsTcs;

    public event EventHandler<string>? ConsoleMessageOccured;
    public event EventHandler<string>? ErrorMessageOccured;

    private string? PythonPath { get; } = pythonService?.GetPythonPath() ?? throw new ArgumentNullException(nameof(pythonService.GetPythonPath));

    public Task<bool> ValidateApiKeyAsync(string apiKey)
    {
        if (pythonService == null)
            throw new NullReferenceException("Python service is null.");

        return pythonService.ExecuteAsync<bool>(() =>
        {
            dynamic sys = Py.Import("sys");
            sys.path.append("Scripts");

            dynamic apiModule = Py.Import("openAI_v2_apiHandler"); // same api module

            var result = apiModule.validate_api_key(apiKey);

            if (result == null)
            {
                throw new InvalidOperationException("<APIH oAI_o1> Validation result is null, check Python API.");
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

            dynamic apiModule = Py.Import("openAI_v2_apiHandler"); // same api module

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

        if (!(settings is OpenAI_o1_ModelSettings))
        {
            throw new ArgumentException("<APIH oAI_o1> Settings must be of type OpenAI_o1_ModelSettings!");
        }

        return await Task.Run(async () =>
        {
            if (_gradioProcess is { HasExited: false })
            {
                return (_localUrl, _publicUrl);
            }

            // Extract parameters for o1-line models
            
            // Extract the drop checkbox value
            var scornReasoningEffort = settings.Parameters
                .OfType<BoolParameter>()
                .FirstOrDefault(p => p.Name == "Scorn RE parameter")?.ValueTyped ?? false;
            
            // //  add reasoning_effort if scornReasoningEffort is false.
            // if (!scornReasoningEffort)
            // {
            //     var reasoningEffort = settings.Parameters
            //         .OfType<StringParameter>()
            //         .FirstOrDefault(p => p.Name == "Reasoning Effort")?.ValueTyped ?? "medium";
            // }

            var maxCompletionTokens = settings.Parameters
                .OfType<IntParameter>()
                .FirstOrDefault(p => p.Name == "Max Completion Tokens")?.ValueTyped;

            var argumentsBuilder = new StringBuilder();

            // PATH handling for the Python script
            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "openAI_o1-line_gradioServer.py");
            OnConsoleMessageOccured($"<APIH oAI_o1> scriptPath is set to: {scriptPath}");
            scriptPath = scriptPath.Replace("\\", "/"); // correct path format for all OS
            OnConsoleMessageOccured($"<APIH oAI_o1> scriptPath was reformatted into: {scriptPath}");

            argumentsBuilder.Append($"-u \"{scriptPath}\" --start-gradio");
            argumentsBuilder.Append($" --api_key \"{apiKey}\"");
            argumentsBuilder.Append($" --model \"{settings.SelectedModel}\"");
            
            //Note by Moe: Many of the models will react with error 400 if there is a reasoning effort parameter in the request!
            //argumentsBuilder.Append($" --reasoning_effort \"{reasoningEffort}\"");
            


            //  add reasoning_effort if scornReasoningEffort is false.
            if (!scornReasoningEffort)
            {
                var reasoningEffort = settings.Parameters
                    .OfType<StringParameter>()
                    .FirstOrDefault(p => p.Name == "Reasoning Effort")?.ValueTyped ?? "medium";
                argumentsBuilder.Append($" --reasoning_effort \"{reasoningEffort}\"");
            }


            if (maxCompletionTokens.HasValue)
            {
                argumentsBuilder.Append($" --max_completion_tokens {maxCompletionTokens.Value}");
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
                throw new Exception("<APIH oAI_o1> Calling from not implemented RuntimeInformation.OSPlatform.");
            }

            OnConsoleMessageOccured($"<APIH oAI_o1> pythonExecutable is set to: {pythonExecutable}");

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
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
                    OnErrorMessageOccured("<internal error APIH oAI_o1> " + args.Data);
                }
            };

            try
            {
                _gradioProcess.Start();
                _gradioProcess.BeginOutputReadLine();
                _gradioProcess.BeginErrorReadLine();

                var completedTask = await Task.WhenAny(_gradioUrlsTcs.Task, Task.Delay(20000)); // wait 20s max

                if (completedTask == _gradioUrlsTcs.Task)
                {
                    return await _gradioUrlsTcs.Task;
                }
                else
                {
                    _publicUrl = "<internal error APIH oAI_o1>";
                    return (_localUrl, _publicUrl);
                }
            }
            catch (Exception ex)
            {
                OnErrorMessageOccured(ex.Message);
                return ("<internal error APIH oAI_o1>", "<internal error APIH oAI_o1>");
            }
        });
    }

    public Task<string> StopGradioInterfaceAsync()
    {
        return Task.Run(() =>
        {
            if (_gradioProcess == null || _gradioProcess.HasExited)
            {
                return "<APIH oAI_o1> Gradio interface not running.";
            }

            try
            {
                _gradioProcess.Kill();
                _gradioProcess.WaitForExit();
                _gradioProcess = null;
                return "<APIH oAI_o1> Gradio interface stopped successfully.";
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
            throw new NullReferenceException("pythonService is null.");
        pythonService.Dispose();

        if (_gradioProcess is { HasExited: false })
        {
            _gradioProcess.Kill();
        }
    }
}
