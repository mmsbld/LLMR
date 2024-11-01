using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMR.Models.ModelSettingsManager;

namespace LLMR.Services.OpenAI_Multicaller;

public class OpenAI_Multicaller_APIService : IAPIService
{
    private readonly OpenAI_Multicaller_APIHandler _apiHandler;

    public event EventHandler<string> ConsoleMessageOccured;
    public event EventHandler<string> ErrorMessageOccured;

    public OpenAI_Multicaller_APIService(IAPIHandler? apiHandler)
    {
        if (apiHandler.GetType() != typeof(OpenAI_Multicaller_APIHandler))
            throw new NotSupportedException("<OAI Multicaller APIS>: IAPIH instance must be of the correct type.");
        
        _apiHandler = (OpenAI_Multicaller_APIHandler?)apiHandler;
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

    public async Task<string> RunMulticallerAsync(string apiKey, IModelSettings? settings)
    {
        return await _apiHandler.RunMulticallerAsync(apiKey, settings);
    }

    public async Task<(string LocalUrl, string PublicUrl)> StartGradioInterfaceAsync(string apiKey,
        IModelSettings? settings)
    {
        throw new NotImplementedException("<OAI MulticallerAPIS: no gradio interface in multicaller mode.>");
    }

    public async Task<string> StopGradioInterfaceAsync()
    {
        throw new NotImplementedException("<OAI MulticallerAPIS: no gradio interface in multicaller mode.>");
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