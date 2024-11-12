using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMR.Model.ModelSettingModulesManager;

namespace LLMR.Services;

public interface IAPIHandler
{
    public event EventHandler<string> ConsoleMessageOccured;
    public event EventHandler<string> ErrorMessageOccured;

    public Task<bool> ValidateApiKeyAsync(string apiKey);
    public Task<List<string>> GetAvailableModelsAsync(string apiKey);

    public Task<(string LocalUrl, string PublicUrl)> StartGradioInterfaceAsync(string apiKey,
        IModelSettings? settings);

    public Task<string> StopGradioInterfaceAsync();

    public void Dispose();
}