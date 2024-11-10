using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using LLMR.Helpers;
using LLMR.Model.ModelSettingModulesManager;
using LLMR.Model.ModelSettingModulesManager.ModelParameters;
using LLMR.Model.ModelSettingModulesManager.ModelSettingsModules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;

namespace LLMR.Model.ChatHistoryManager
{
    public class ChatHistoryCollection : ReactiveObject
    {
        public ObservableCollection<ChatHistoryCategory> Categories { get; set; } = new ObservableCollection<ChatHistoryCategory>();
        
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

        // **Füge die fehlenden Ereignisse hinzu**
        public event EventHandler<string>? ConsoleMessageOccurred;
        public event EventHandler<string>? ExceptionOccurred;
        

        // Methode zum Extrahieren eingebetteter Skripte
        private void ExtractEmbeddedScripts(string targetDirectory)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames();

            foreach (var resourceName in resourceNames)
            {
                if (resourceName.EndsWith(".py"))
                {
                    string fileName = Path.GetFileName(resourceName);
                    string filePath = Path.Combine(targetDirectory, fileName);
            
                    // Loggen des Extraktionsprozesses
                    ConsoleMessageOccurred?.Invoke(this, $"Extrahiere Skript: {fileName} nach {filePath}");

                    if (!File.Exists(filePath))
                    {
                        using (var stream = assembly.GetManifestResourceStream(resourceName))
                        {
                            if (stream == null)
                            {
                                ConsoleMessageOccurred?.Invoke(this, $"Ressourcenstrom für {resourceName} ist null.");
                                continue;
                            }

                            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                            {
                                stream.CopyTo(fileStream);
                            }

                            ConsoleMessageOccurred?.Invoke(this, $"Skript {fileName} erfolgreich extrahiert.");
                        }
                    }
                    else
                    {
                        ConsoleMessageOccurred?.Invoke(this, $"Skript {fileName} existiert bereits in {filePath}.");
                    }
                }
            }
        }
        public ChatHistoryCollection()
        {
            string baseDataDir = AppDataPath.GetBaseDataDirectory();
            ConsoleMessageOccurred?.Invoke(this, $"Basisdatenverzeichnis: {baseDataDir}");
            ExtractEmbeddedScripts(baseDataDir);

            // Pfad zum chat_histories-Verzeichnis
            string chatHistoriesDir = Path.Combine(baseDataDir, "chat_histories");

            // Überprüfen und Erstellen des chat_histories-Verzeichnisses
            if (!Directory.Exists(chatHistoriesDir))
            {
                Directory.CreateDirectory(chatHistoriesDir);
                ConsoleMessageOccurred?.Invoke(this, $"Erstelle Verzeichnis: {chatHistoriesDir}");
            }
            else
            {
                ConsoleMessageOccurred?.Invoke(this, $"Verzeichnis existiert bereits: {chatHistoriesDir}");
            }

            // Lade die Dateien aus dem chat_histories-Verzeichnis
            LoadFiles(chatHistoriesDir);
        }

        public void AddFolder(string folderName)
        {
            var baseDirectory = Path.Combine(AppDataPath.GetBaseDataDirectory(), "chat_histories");
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
            string baseDirectory = Path.Combine(AppDataPath.GetBaseDataDirectory(), "chat_histories");

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
                    LoadFiles(baseDirectory);
                }
            }
            else if (item is ChatHistoryFile file)
            {
                if (File.Exists(file.FullPath))
                {
                    File.Delete(file.FullPath);
                    LoadFiles(baseDirectory);
                }
            }
        }

        public void RenameItem(object? item, string newName)
        {
            string baseDirectory = Path.Combine(AppDataPath.GetBaseDataDirectory(), "chat_histories");

            if (item is ChatHistoryCategory category)
            {
                if (category.ParentCategory == null)
                {
                    throw new InvalidOperationException("Cannot rename the root category.");
                }

                var newDirectoryPath = Path.Combine(Path.GetDirectoryName(category.FullPath) ?? baseDirectory, newName);

                if (!Directory.Exists(newDirectoryPath))
                {
                    Directory.Move(category.FullPath, newDirectoryPath);
                    category.Name = newName;
                    category.FullPath = newDirectoryPath;
                    LoadFiles(baseDirectory);
                }
                else
                {
                    throw new IOException($"Folder '{newName}' already exists.");
                }
            }
            else if (item is ChatHistoryFile file)
            {
                var newFilePath = Path.Combine(Path.GetDirectoryName(file.FullPath) ?? baseDirectory, newName);

                if (!File.Exists(newFilePath))
                {
                    File.Move(file.FullPath, newFilePath);
                    file.Filename = newName;
                    file.FullPath = newFilePath;
                    LoadFiles(baseDirectory);
                }
                else
                {
                    throw new IOException($"File '{newName}' already exists.");
                }
            }
        }

        public void LoadFiles(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                // Falls das Verzeichnis nicht existiert, erstelle es
                Directory.CreateDirectory(directoryPath);
                ConsoleMessageOccurred?.Invoke(this, $"Erstelle Verzeichnis: {directoryPath}");
            }
  
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
            // load directories (& subfolders)
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
                    ExceptionOccurred?.Invoke(this, $"<CHC> Unable to load file: {ex.Message}");
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
                    // JSON-Format Kompatibilität für ältere Versionen
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
                            Label = null,  // kein benutzerdefiniertes Label in regulären Konversationen
                            User = entry.user,
                            Assistant = entry.assistant
                        });
                    }
                }
                else if (jsonData.responses != null)
                {
                    // Multicaller-Format:
                    string userPrompt = jsonData.settings.prompt;
                    int totalResponses = jsonData.responses.Count;

                    // Benutzer-Prompt einmal hinzufügen
                    Conversation.Add(new ConversationEntry
                    {
                        Label = "User (Prompt):",
                        User = userPrompt,
                        Assistant = null  // keine Assistentenantwort in MC
                    });

                    // Alle Assistentenantworten hinzufügen
                    for (int i = 0; i < totalResponses; i++)
                    {
                        var entry = jsonData.responses[i];
                        string assistantResponse = entry.assistant != null ? entry.assistant.ToString() : $"Error: {entry.error}";

                        Conversation.Add(new ConversationEntry
                        {
                            Label = $"Assistant {i + 1}/{totalResponses}:",
                            User = null,  // keine unnötige Wiederholung der Benutzernachricht für Assistentenantworten
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
                // Inhalt zurücksetzen, um das Anzeigen des vorherigen Inhalts in der UI zu vermeiden
                Settings = null;
                Conversation.Clear();
                SelectedFile = null;

                ExceptionOccurred?.Invoke(this, $"<CHC> Unable to load chat history file: {filePath}, Error: {ex.Message}");
                throw new Exception("<CHC> Unable to load chat history file: " + filePath, ex);
            }
        }
    }
}
