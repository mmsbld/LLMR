using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMR.Models.ModelSettingsManager;

namespace LLMR.Services;

public interface IAPIService : IDisposable
{
    event EventHandler<string> ConsoleMessageOccured;
    event EventHandler<string> ErrorMessageOccured;
    
    Task<bool> ValidateApiKeyAsync(string apiKey);
    Task<List<string>> GetAvailableModelsAsync(string apiKey);
    Task<(string LocalUrl, string PublicUrl)> StartGradioInterfaceAsync(string apiKey, IModelSettings? settings);
    Task<string> StopGradioInterfaceAsync();
}