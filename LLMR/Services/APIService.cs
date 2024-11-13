using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMR.Model.ModelSettingModulesManager;
using LLMR.Model.ModelSettingModulesManager.ModelSettingsModules;

namespace LLMR.Services;

public sealed class APIService:IAPIService
{
    private readonly IAPIHandler? _apiHandler;

    public event EventHandler<string>? ConsoleMessageOccured;
    public event EventHandler<string>? ErrorMessageOccured;

    public APIService(IAPIHandler? apiHandler)
    {
        _apiHandler = apiHandler;
        if (_apiHandler == null)
            throw new ArgumentNullException(nameof(_apiHandler));
        _apiHandler.ConsoleMessageOccured += HandleConsoleMessageOccured;
        _apiHandler.ErrorMessageOccured += HandleErrorMessageOccured;
    }

    private void HandleErrorMessageOccured(object? sender, string message)
    {
        OnErrorMessageOccured(message);
    }

    private void HandleConsoleMessageOccured(object? sender, string consoleMessage)
    {
        OnConsoleMessageOccured(consoleMessage);
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey)
    {
        if (_apiHandler == null)
            throw new NullReferenceException(nameof(_apiHandler));
        return await _apiHandler.ValidateApiKeyAsync(apiKey);
    }

    public async Task<List<string>> GetAvailableModelsAsync(string apiKey)
    {
        if (_apiHandler == null)
            throw new NullReferenceException(nameof(_apiHandler));
        return await _apiHandler.GetAvailableModelsAsync(apiKey);
    }

    public async Task<(string LocalUrl, string PublicUrl)> StartGradioInterfaceAsync(string apiKey, IModelSettings? settings)
    {
        if (_apiHandler == null)
            throw new NullReferenceException(nameof(_apiHandler));
        return await _apiHandler.StartGradioInterfaceAsync(apiKey, settings);
    }

    public async Task<string> StopGradioInterfaceAsync()
    {
        if (_apiHandler == null)
            throw new NullReferenceException(nameof(_apiHandler));
        return await _apiHandler.StopGradioInterfaceAsync();
    }
    
    public async Task<string> RunMulticallerAsync(string apiKey, IModelSettings? settings)
    {
        if (settings is not OpenAI_Multicaller_ModelSettings)
        {
            throw new ArgumentException("Settings must be of type OpenAI_Multicaller_ModelSettings!");
        }
        if (_apiHandler == null)
            throw new NullReferenceException(nameof(_apiHandler));
        return await _apiHandler.RunMulticallerAsync(apiKey, settings);
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
        if (_apiHandler == null)
            throw new NullReferenceException(nameof(_apiHandler));
        _apiHandler.Dispose();
    }
}