using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using LLMR.Helpers;
using LLMR.Model.ModelSettingModulesManager;
using LLMR.Model.ModelSettingModulesManager.ModelParameters;
using LLMR.Model.ModelSettingModulesManager.ModelSettingsModules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;

namespace LLMR.Model.ChatHistoryManager;

public class ChatHistoryCollection : ReactiveObject
{
    public ObservableCollection<ChatHistoryCategory> Categories { get; set; } = new ObservableCollection<ChatHistoryCategory>();

    private object? _selectedItem;
    public object? SelectedItem
    {
        get => _selectedItem;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedItem, value);
            if (value is ChatHistoryFile file)
            {
                LoadFileContent(file);
            }
            else
            {
                // Clear details if a folder is selected.
                Settings = null;
                ApiKey = null;
                DownloadedOn = null;
                Conversation.Clear();
            }
        }
    }

    private string? _apiKey;
    public string? ApiKey
    {
        get => _apiKey;
        set => this.RaiseAndSetIfChanged(ref _apiKey, value);
    }

    private string? _downloadedOn;
    public string? DownloadedOn
    {
        get => _downloadedOn;
        set => this.RaiseAndSetIfChanged(ref _downloadedOn, value);
    }

    private IModelSettings? _settings;
    public IModelSettings? Settings
    {
        get => _settings;
        set => this.RaiseAndSetIfChanged(ref _settings, value);
    }

    private ObservableCollection<ConversationEntry> _conversation = new ObservableCollection<ConversationEntry>();
    public ObservableCollection<ConversationEntry> Conversation
    {
        get => _conversation;
        set => this.RaiseAndSetIfChanged(ref _conversation, value);
    }

    public event EventHandler<string>? ConsoleMessageOccurred;
    public event EventHandler<string>? ExceptionOccurred;

    private string? _directoryPath;

    public ChatHistoryCollection()
    {
        var baseDataDir = PathManager.GetBaseDirectory();
        ConsoleMessageOccurred?.Invoke(this, $"Base directory: {baseDataDir}");

        var chatHistoriesDir = PathManager.Combine(baseDataDir, "Scripts", "chat_histories");

        if (!Directory.Exists(chatHistoriesDir))
        {
            Directory.CreateDirectory(chatHistoriesDir);
            ConsoleMessageOccurred?.Invoke(this, $"Created directory: {chatHistoriesDir}");
        }
        else
        {
            ConsoleMessageOccurred?.Invoke(this, $"Directory already exists: {chatHistoriesDir}");
        }

        _directoryPath = chatHistoriesDir;

        LoadFiles();
    }

    public void AddFolder(string folderName)
    {
        var newFolderPath = PathManager.Combine(_directoryPath, folderName);

        if (!Directory.Exists(newFolderPath))
        {
            Directory.CreateDirectory(newFolderPath);
            LoadFiles(); // reload files (UI update)
        }
        else
        {
            throw new IOException($"<CHC> Directory '{folderName}' already exists.");
        }
    }

    public void RemoveItem(object? item)
    {
        switch (item)
        {
            case ChatHistoryCategory category when category.ParentCategory == null:
                throw new InvalidOperationException("<CHC> Cannot remove the root category.");
            case ChatHistoryCategory category when !Directory.Exists(category.FullPath):
                return;
            case ChatHistoryCategory category:
                Directory.Delete(category.FullPath, true);
                LoadFiles();
                break;
            case ChatHistoryFile file:
                if (File.Exists(file.FullPath))
                {
                    File.Delete(file.FullPath);
                    LoadFiles();
                }
                break;
        }
        LoadFiles();
    }

    public void RenameItem(object? item, string newName)
    {
        switch (item)
        {
            case ChatHistoryCategory category when category.ParentCategory == null:
                throw new InvalidOperationException("<CHC> Cannot rename the root category.");
            case ChatHistoryCategory category:
            {
                var parentDir = Path.GetDirectoryName(category.FullPath) ?? _directoryPath;
                var newDirectoryPath = PathManager.Combine(parentDir, newName);

                if (!Directory.Exists(newDirectoryPath))
                {
                    Directory.Move(category.FullPath, newDirectoryPath);
                    category.Name = newName;
                    category.FullPath = newDirectoryPath;
                }
                else
                {
                    throw new IOException($"<CHC> Folder '{newName}' already exists.");
                }
                break;
            }
            case ChatHistoryFile file:
            {
                var parentDir = Path.GetDirectoryName(file.FullPath) ?? _directoryPath;
                var newFilePath = PathManager.Combine(parentDir, newName);

                if (!File.Exists(newFilePath))
                {
                    File.Move(file.FullPath, newFilePath);
                    file.Filename = newName;
                    file.FullPath = newFilePath;
                }
                else
                {
                    throw new IOException($"<CHC> File '{newName}' already exists.");
                }
                break;
            }
        }
        LoadFiles();
    }

    public void LoadFiles()
    {
        if (!Directory.Exists(_directoryPath))
        {
            Directory.CreateDirectory(_directoryPath);
            ConsoleMessageOccurred?.Invoke(this, $"Created folder: {_directoryPath}");
        }

        Categories.Clear();

        var rootCategory = new ChatHistoryCategory
        {
            Name = "Chat Histories",
            FullPath = _directoryPath,
            ParentCategory = null
        };

        LoadItemsFromDirectory(_directoryPath, rootCategory);
        Categories.Add(rootCategory);
    }

    private void LoadItemsFromDirectory(string directoryPath, ChatHistoryCategory parentCategory)
    {
        // Load directories and subfolders.
        var directories = Directory.GetDirectories(directoryPath);
        foreach (var dir in directories)
        {
            var dirInfo = new DirectoryInfo(dir);
            var subCategory = new ChatHistoryCategory
            {
                Name = dirInfo.Name,
                FullPath = dir,
                ParentCategory = parentCategory
            };

            LoadItemsFromDirectory(dir, subCategory);
            parentCategory.Items.Add(subCategory);
        }

        // Load files.
        var files = Directory.GetFiles(directoryPath, "*.json");
        foreach (var file in files)
        {
            try
            {
                var fileContent = File.ReadAllText(file);
                dynamic jsonData = JsonConvert.DeserializeObject(fileContent);

                string downloadedOnStr = jsonData.settings.downloaded_on;
                var downloadedOn = DateTime.ParseExact(downloadedOnStr, "MMMM dd, yyyy 'at' HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

                var chatHistoryFile = new ChatHistoryFile
                {
                    Filename = Path.GetFileName(file),
                    DownloadedOn = downloadedOn,
                    FullPath = file
                };

                parentCategory.Items.Add(chatHistoryFile);
            }
            catch (Exception ex)
            {
                ExceptionOccurred?.Invoke(this, $"<CHC> Unable to load file: {ex.Message}");
                throw new ArgumentException("<CHC> Unable to load file: " + ex.Message);
            }
        }
    }

    private void LoadFileContent(ChatHistoryFile file)
    {
        var filePath = file.FullPath;
        if (!File.Exists(filePath)) return;

        try
        {
            var fileContent = File.ReadAllText(filePath);
            dynamic jsonData = JsonConvert.DeserializeObject(fileContent);

            var modelSettings = new GenericModelSettings
            {
                SelectedModel = jsonData.settings.model
            };

            JObject parametersData = jsonData.settings.parameters;
            if (parametersData == null)
            {
                parametersData = new JObject();
                foreach (var prop in jsonData.settings)
                {
                    string propName = prop.Name;
                    if (propName != "model" && propName != "api_key" && propName != "downloaded_on")
                    {
                        parametersData[propName] = prop.Value;
                    }
                }
            }

            foreach (var param in parametersData)
            {
                var paramName = param.Key;
                var paramValue = param.Value;
                ModelParameter modelParam;

                switch (paramValue.Type)
                {
                    case JTokenType.Integer:
                        modelParam = new IntParameter
                        {
                            Name = paramName,
                            Value = paramValue.Value<int>(),
                            Min = null,
                            Max = null
                        };
                        break;
                    case JTokenType.Float:
                        modelParam = new DoubleParameter
                        {
                            Name = paramName,
                            Value = paramValue.Value<double>(),
                            Min = null,
                            Max = null,
                            Increment = null
                        };
                        break;
                    case JTokenType.String:
                        modelParam = new StringParameter
                        {
                            Name = paramName,
                            Value = paramValue.Value<string>()
                        };
                        break;
                    case JTokenType.Boolean:
                        modelParam = new BoolParameter
                        {
                            Name = paramName,
                            Value = paramValue.Value<bool>()
                        };
                        break;
                    default:
                        modelParam = new StringParameter
                        {
                            Name = paramName,
                            Value = paramValue.ToString()
                        };
                        break;
                }

                modelSettings.Parameters.Add(modelParam);
            }

            Settings = modelSettings;
            ApiKey = jsonData.settings.api_key;
            DownloadedOn = jsonData.settings.downloaded_on;
            Conversation.Clear();

            if (jsonData.conversation != null)
            {
                foreach (var entry in jsonData.conversation)
                {
                    Conversation.Add(new ConversationEntry
                    {
                        Label = null,
                        User = entry.user,
                        Assistant = entry.assistant
                    });
                }
            }
            else if (jsonData.responses != null)
            {
                string userPrompt = jsonData.settings.prompt;
                int totalResponses = jsonData.responses.Count;

                Conversation.Add(new ConversationEntry
                {
                    Label = "User (Prompt):",
                    User = userPrompt,
                    Assistant = null
                });

                for (var i = 0; i < totalResponses; i++)
                {
                    var entry = jsonData.responses[i];
                    string assistantResponse = entry.assistant != null ? entry.assistant.ToString() : $"<CHC> Error: {entry.error}";

                    Conversation.Add(new ConversationEntry
                    {
                        Label = $"Assistant {i + 1}/{totalResponses}:",
                        User = null,
                        Assistant = assistantResponse
                    });
                }
            }
            else
            {
                throw new ArgumentException("<CHC> Error while loading chat history collection! Found nothing (no entries - old file format?!) : " + filePath);
            }
        }
        catch (Exception ex)
        {
            Settings = null;
            Conversation.Clear();
            SelectedItem = null;
            ExceptionOccurred?.Invoke(this, $"<CHC> Unable to load chat history file: {filePath}, Error: {ex.Message}");
            throw new Exception("<CHC> Unable to load chat history file: " + filePath, ex);
        }
    }

    // Recursively sorts files (ChatHistoryFile) by DownloadedOn (newest first) in every category.
    public void SortFilesByDate()
    {
        foreach (var category in Categories)
        {
            SortItemsInCategory(category);
        }
    }

    private void SortItemsInCategory(ChatHistoryCategory category)
    {
        var folders = category.Items.OfType<ChatHistoryCategory>().OrderBy(c => c.Name).ToList();
        var files = category.Items.OfType<ChatHistoryFile>().OrderByDescending(f => f.DownloadedOn).ToList();

        category.Items.Clear();

        foreach (var folder in folders)
        {
            category.Items.Add(folder);
            SortItemsInCategory(folder);
        }
        foreach (var file in files)
        {
            category.Items.Add(file);
        }
    }

    // Moves a file to a different folder on disk and refreshes the file list.
    public void MoveFileToFolder(ChatHistoryFile file, ChatHistoryCategory targetCategory)
    {
        if (!File.Exists(file.FullPath))
            throw new FileNotFoundException($"File {file.Filename} not found.");

        var newPath = Path.Combine(targetCategory.FullPath, file.Filename);
        if (File.Exists(newPath))
            throw new IOException($"A file with name {file.Filename} already exists in {targetCategory.Name}.");

        File.Move(file.FullPath, newPath);
        file.FullPath = newPath;
        LoadFiles();
    }
}