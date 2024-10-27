using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMR.Models.ModelSettingsManager;

namespace LLMR.Services;

public class APIService:IAPIService
{
    private readonly IAPIHandler _apiHandler;

    public event EventHandler<string> ConsoleMessageOccured;
    public event EventHandler<string> ErrorMessageOccured;

    public APIService(IAPIHandler apiHandler)
    {
        _apiHandler = apiHandler;
        _apiHandler.ConsoleMessageOccured += HandleConsoleMessageOccured;
        _apiHandler.ErrorMessageOccured += HandleErrorMessageOccured;
    }

    private void HandleErrorMessageOccured(object sender, string message)
    {
        OnErrorMessageOccured(message);
    }

    private void HandleConsoleMessageOccured(object sender, string consoleMessage)
    {
        OnConsoleMessageOccured(consoleMessage);
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey)
    {
        return await _apiHandler.ValidateApiKeyAsync(apiKey);
    }

    public async Task<List<string>> GetAvailableModelsAsync(string apiKey)
    {
        return await _apiHandler.GetAvailableModelsAsync(apiKey);
    }

    public async Task<(string LocalUrl, string PublicUrl)> StartGradioInterfaceAsync(string apiKey, IModelSettings settings)
    {
        return await _apiHandler.StartGradioInterfaceAsync(apiKey, settings);
    }

    public async Task<string> StopGradioInterfaceAsync()
    {
        return await _apiHandler.StopGradioInterfaceAsync();
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
        _apiHandler.Dispose();
    }
}