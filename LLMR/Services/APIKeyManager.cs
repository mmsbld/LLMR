using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using LLMR.Helpers;
using LLMR.Models;
using Newtonsoft.Json;

namespace LLMR.Services;

/// <summary>
/// Is used to saves and load API keys that are needed to connect to an API (e.g. an OpenAI key or a Hugging Face Token). 
/// </summary>
public class APIKeyManager
{
    private readonly string _filePath;

    /// <summary>
    /// All API keys that are needed to connect to an API (e.g. an OpenAI key or a Hugging Face token).
    /// </summary>
    public ObservableCollection<APIKeyEntry?> SavedApiKeys { get; } = [];

    /// <summary>
    /// Initialize an object of type APIKeyManager and loads API keys. 
    /// </summary>
    public APIKeyManager()
    {
        _filePath = PathManager.Combine(AppDomain.CurrentDomain.BaseDirectory, "apikeys.json");
        LoadApiKeys();
    }

    private void LoadApiKeys()
    {
        if (!File.Exists(_filePath)) return;
        var json = File.ReadAllText(_filePath);
        var keys = JsonConvert.DeserializeObject<List<APIKeyEntry>>(json);
        if (keys == null) return;
        foreach (var key in keys)
        {
            SavedApiKeys.Add(key);
        }
    }
    
    /// <summary>
    /// Save all API keys. 
    /// </summary>
    private void SaveAPIKeys()
    {
        var json = JsonConvert.SerializeObject(SavedApiKeys, Formatting.Indented);
        File.WriteAllText(_filePath, json);
    }

    /// <summary>
    /// Opens a dialog that  asks the user for a name and an API key/token. 
    /// </summary>
    /// <param name="promptUserAsync">Name and API key/token</param>
    public async Task AddNewAPIKeyAsync(Func<string, Task<string>> promptUserAsync)
    {
        var name = await promptUserAsync("Enter a name for the API key:");
        if (string.IsNullOrEmpty(name))
        {
            ConsoleMessageManager.LogInfo("API key addition canceled by the user.");
            return;
        }

        var key = await promptUserAsync("Enter the API key:");
        if (string.IsNullOrEmpty(key))
        {
            ConsoleMessageManager.LogInfo("API key addition canceled by the user.");
            return;
        }

        var newEntry = new APIKeyEntry { Name = name, Key = key };
        SavedApiKeys.Add(newEntry);
        SaveAPIKeys();
        ConsoleMessageManager.LogInfo($"API key '{name}' added successfully.");
    }

    /// <summary>
    /// Removes an API key/token.
    /// </summary>
    /// <param name="apiKey">the APIKeyEntry that shall be removed</param>
    public void RemoveAPIKey(APIKeyEntry? apiKey)
    {
        if (apiKey == null) return;
        SavedApiKeys.Remove(apiKey);
        SaveAPIKeys();
        ConsoleMessageManager.LogInfo($"API key '{apiKey.Name}' removed successfully.");
    }

    /// <summary>
    /// Marks the selected API key/token as the one that was l
    /// </summary>
    /// <param name="selectedApiKey"></param>
    public void UpdateLastUsedAPIKey(APIKeyEntry? selectedApiKey)
    {
        foreach (var apiKey in SavedApiKeys)
        {
            if (apiKey != null)
            {
                apiKey.IsLastUsed = apiKey == selectedApiKey;
            }
        }
        SaveAPIKeys();
    }
}