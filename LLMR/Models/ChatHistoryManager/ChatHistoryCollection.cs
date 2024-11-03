using System;
using System.Collections.ObjectModel;
using System.IO;
using LLMR.Models.ModelSettingsManager;
using LLMR.Models.ModelSettingsManager.ModelParameters;
using LLMR.Models.ModelSettingsManager.ModelSettingsModules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;

namespace LLMR.Models.ChatHistoryManager;

public class ChatHistoryCollection : ReactiveObject
{
    public ObservableCollection<ChatHistoryCategory> Categories { get; set; } = [];
    
    private ChatHistoryFile? _selectedFile;
    private string? _apiKey;
    
    public ChatHistoryFile? SelectedFile
    {
        get => _selectedFile;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedFile, value);
            LoadFileContent(value);
        }
    }

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
    
    public void AddFolder(string folderName)
    {
        var baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "chat_histories");
        var newFolderPath = Path.Combine(baseDirectory, folderName);

        if (!Directory.Exists(newFolderPath))
        {
            Directory.CreateDirectory(newFolderPath);
            LoadFiles(baseDirectory); // Reload the files to update the UI
        }
        else
        {
            throw new IOException($"Folder '{folderName}' already exists.");
        }
    }

    public void RemoveItem(object? item)
    {
        if (item is ChatHistoryCategory category)
        {
            // Cannot remove root categories
            if (category.ParentCategory == null)
            {
                throw new InvalidOperationException("Cannot remove the root category.");
            }

            if (Directory.Exists(category.FullPath))
            {
                Directory.Delete(category.FullPath, true);
                LoadFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "chat_histories"));
            }
        }
        else if (item is ChatHistoryFile file)
        {
            if (File.Exists(file.FullPath))
            {
                File.Delete(file.FullPath);
                LoadFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "chat_histories"));
            }
        }
    }

    public void RenameItem(object? item, string newName)
    {
        if (item is ChatHistoryCategory category)
        {
            if (category.ParentCategory == null)
            {
                throw new InvalidOperationException("Cannot rename the root category.");
            }

            var newDirectoryPath = Path.Combine(Path.GetDirectoryName(category.FullPath), newName);

            if (!Directory.Exists(newDirectoryPath))
            {
                Directory.Move(category.FullPath, newDirectoryPath);
                category.Name = newName;
                category.FullPath = newDirectoryPath;
                LoadFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "chat_histories"));
            }
            else
            {
                throw new IOException($"Folder '{newName}' already exists.");
            }
        }
        else if (item is ChatHistoryFile file)
        {
            var newFilePath = Path.Combine(Path.GetDirectoryName(file.FullPath), newName);

            if (!File.Exists(newFilePath))
            {
                File.Move(file.FullPath, newFilePath);
                file.Filename = newName;
                file.FullPath = newFilePath;
                LoadFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "chat_histories"));
            }
            else
            {
                throw new IOException($"File '{newName}' already exists.");
            }
        }
    }

    public void LoadFiles(string directoryPath)
    {
        Categories.Clear();

        var rootCategory = new ChatHistoryCategory
        {
            Name = "Chat Histories",
            FullPath = directoryPath,
            ParentCategory = null
        };

        LoadItemsFromDirectory(directoryPath, rootCategory);

        Categories.Add(rootCategory);
    }
    
    private void LoadItemsFromDirectory(string directoryPath, ChatHistoryCategory parentCategory)
    {
        // load directories (&subfolders)
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

        // load files
        var files = Directory.GetFiles(directoryPath, "*.json");

        foreach (var file in files)
        {
            try
            {
                var fileContent = File.ReadAllText(file);
                dynamic jsonData = JsonConvert.DeserializeObject(fileContent);

                string downloadedOnStr = jsonData.settings.downloaded_on;
                DateTime downloadedOn = DateTime.ParseExact(downloadedOnStr, "MMMM dd, yyyy 'at' HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

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
                throw new ArgumentException("<CHC> Unable to load file: " + ex.Message);
            }
        }
    }
    

    private void LoadFileContent(ChatHistoryFile? file)
    {
        if (file == null) return;

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
                // Comment<Moe>: pre v. 0.3 alpha JSON format compatibility: 
                // old format (parameters as direct properties)
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
                        Label = null,  // no custom label in regular conversations!
                        User = entry.user,
                        Assistant = entry.assistant
                    });
                }
            }
            else if (jsonData.responses != null)
            {
                // Multicaller format:
                string userPrompt = jsonData.settings.prompt;
                int totalResponses = jsonData.responses.Count;

                // add the user prompt once
                Conversation.Add(new ConversationEntry
                {
                    Label = "User (Prompt):",
                    User = userPrompt,
                    Assistant = null  // no assistant response in MC
                });

                // add all assistant responses
                for (int i = 0; i < totalResponses; i++)
                {
                    var entry = jsonData.responses[i];
                    string assistantResponse = entry.assistant != null ? entry.assistant.ToString() : $"Error: {entry.error}";

                    Conversation.Add(new ConversationEntry
                    {
                        Label = $"Assistant {i + 1}/{totalResponses}:",
                        User = null,  // no useless repetition of the user message for assistant responses
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
            // reset content to avoid showing the previous file's content in UI
            Settings = null;
            Conversation.Clear();
            SelectedFile = null;

            throw new Exception("<CHC> Unable to load chat history file: " + filePath, ex);
        }
    }
}