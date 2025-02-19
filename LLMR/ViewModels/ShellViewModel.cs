using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LLMR.Helpers;
using LLMR.Model;
using LLMR.Model.ChatHistoryManager;
using LLMR.Model.ModelSettingModulesManager;
using LLMR.Model.ModelSettingModulesManager.ModelSettingsModules;
using LLMR.Services;
using LLMR.Services.HFServerlessInference;
using LLMR.Services.OpenAI_Multicaller;
using LLMR.Services.OpenAI_o1;
using LLMR.Services.OpenAI_v2;
using QRCoder;
using QuestPDF.Infrastructure;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QuestPDF.Fluent;
using Unit = System.Reactive.Unit;

namespace LLMR.ViewModels;

/// <summary>
/// The main "shell" for the application. 
/// It manages navigation (via ShellNavigationViewModel),
/// keeps track of Python environment, API keys, console output, server status, etc.
/// Replaces the old MainWindowViewModel but preserves all commands and logic.
/// </summary>
public class ShellViewModel : ViewModelBase, IDisposable
{
    private readonly PythonEnvironmentInitializer _pythonEnvironmentInitializer;
    private readonly APIKeyManager _apiKeyManager;
    private readonly DialogService _dialogService;
    private PythonExecutionService? _pythonService;
    private bool _pythonRunning;
    private bool _pythonInitSuccess;

    private string? _serverStatus;
    private IImmutableSolidColorBrush? _serverStatusColor;
    private string? _apiKey;
    private string? _pythonPath;
    private bool _isBusy;
    private bool _isConsoleExpanded;
    private bool _showTimestamp;
    private int _selectedConsoleIndex;
    private Bitmap? _generatedPublicLinkQRCode;
    private IModelSettings? _modelSettingsModule;
    private string? _selectedModelType;
    private IAPIService? _apiService;
    private bool _isServerRunning;
    private bool _isPythonPathLocked;

    private APIKeyEntry? _selectedApiKey;
    private readonly ObservableAsPropertyHelper<bool> _isApiKeySelected;

    /// <summary>
    /// Manages which screen is currently visible (no more tab indexes).
    /// </summary>
    public ShellNavigationViewModel Navigation { get; }

    /// <summary>
    /// Holds all console messages for the bottom console area.
    /// </summary>
    public ObservableCollection<ConsoleMessage> ConsoleMessages { get; } = new();

    /// <summary>
    /// Holds the chat histories, categories, etc. for data collection.
    /// </summary>
    public ChatHistoryCollection ChatHistoryCollection { get; }

    /// <summary>
    /// The available module “types” (previously a ComboBox) will become a list or gallery. 
    /// You could store more info (icon, description, etc.) for each item if desired.
    /// </summary>
    public ObservableCollection<string> AvailableModuleTypes { get; set; }

    #region Constructors

    public ShellViewModel()
    {
        // Navigation manager
        Navigation = new ShellNavigationViewModel();

        // Services
        _apiKeyManager = new APIKeyManager();
        _dialogService = new DialogService();
        _pythonEnvironmentInitializer = new PythonEnvironmentInitializer();

        // Default
        _pythonRunning = false;
        AvailableModuleTypes = new ObservableCollection<string>
        {
            "OpenAI",
            "OpenAI o1-line",
            "Hugging Face Serverless Inference",
            "OpenAI Multicaller"
        };
        SelectedModelType = "OpenAI"; // default selection

        // Chat history
        ChatHistoryCollection = new ChatHistoryCollection();
        ChatHistoryCollection.ConsoleMessageOccurred += OnConsoleMessageOccurred;
        ChatHistoryCollection.ExceptionOccurred += OnExceptionOccurred;
        LoadChatHistories();

        // Observe selected API key
        _isApiKeySelected = this
            .WhenAnyValue(x => x.SelectedApiKey)
            .Select(apiKey => apiKey != null)
            .ToProperty(this, x => x.IsApiKeySelected);

        // Initialize commands
        InitializeCommands();

        // Some initial states
        ServerStatus = "Stopped";
        ServerStatusColor = Brushes.Red;
        IsBusy = false;

        // Retrieve the last used API key if available
        SelectedApiKey = SavedApiKeys.FirstOrDefault(k => k != null && k.IsLastUsed)
                         ?? SavedApiKeys.FirstOrDefault();

        // QuestPDF license
        QuestPDF.Settings.License = LicenseType.Community;

        // Force navigation to the “Login” screen on startup
        Navigation.NavigateToLogin();

        // Add trace listener for internal logs
        Trace.Listeners.Add(new InternalConsoleTraceListener(msg =>
        {
            var consoleMsg = ConsoleMessageManager.CreateConsoleMessage(msg, MessageType.Debug);
            AddToConsole(consoleMsg);
        }));

        // Some initial system messages
        ConsoleMessageManager.PrintSystemInfo();
        ConsoleMessageManager.PrintNetworkWarning();
        ConsoleMessageManager.PrintWelcomeMessage();
    }

    #endregion

    #region Properties - Server/Python

    public string? ServerStatus
    {
        get => _serverStatus;
        set => this.RaiseAndSetIfChanged(ref _serverStatus, value);
    }

    public IImmutableSolidColorBrush? ServerStatusColor
    {
        get => _serverStatusColor;
        set => this.RaiseAndSetIfChanged(ref _serverStatusColor, value);
    }

    public bool IsServerRunning
    {
        get => _isServerRunning;
        set
        {
            this.RaiseAndSetIfChanged(ref _isServerRunning, value);
            ServerStatus = value ? "Running" : "Stopped";
            ServerStatusColor = value ? Brushes.LimeGreen : Brushes.Red;
        }
    }

    public bool IsPythonPathLocked
    {
        get => _isPythonPathLocked;
        set => this.RaiseAndSetIfChanged(ref _isPythonPathLocked, value);
    }

    public string? PythonPath
    {
        get => _pythonPath;
        set => this.RaiseAndSetIfChanged(ref _pythonPath, value);
    }

    #endregion

    #region Properties - Busy/Console

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            this.RaiseAndSetIfChanged(ref _isBusy, value);
            // Expand the console if we become busy, so user can see logs
            if (!IsConsoleExpanded && value)
                IsConsoleExpanded = true;
        }
    }

    public bool IsConsoleExpanded
    {
        get => _isConsoleExpanded;
        set
        {
            // If busy, always expand console
            if (IsBusy)
            {
                this.RaiseAndSetIfChanged(ref _isConsoleExpanded, true);
                return;
            }
            this.RaiseAndSetIfChanged(ref _isConsoleExpanded, value);
        }
    }

    public bool ShowTimestamp
    {
        get => _showTimestamp;
        set => this.RaiseAndSetIfChanged(ref _showTimestamp, value);
    }

    public int SelectedConsoleIndex
    {
        get => _selectedConsoleIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedConsoleIndex, value);
    }

    #endregion

    #region Properties - API Keys

    public ObservableCollection<APIKeyEntry?> SavedApiKeys => _apiKeyManager.SavedApiKeys;

    public bool IsApiKeySelected => _isApiKeySelected.Value;

    public APIKeyEntry? SelectedApiKey
    {
        get => _selectedApiKey;
        set => this.RaiseAndSetIfChanged(ref _selectedApiKey, value);
    }

    /// <summary>
    /// The actual string for the chosen API Key (private field).
    /// </summary>
    private string? ApiKey
    {
        get => _apiKey;
        set => this.RaiseAndSetIfChanged(ref _apiKey, value);
    }

    #endregion

    #region Properties - Model / Module

    public string? SelectedModelType
    {
        get => _selectedModelType;
        set => this.RaiseAndSetIfChanged(ref _selectedModelType, value);
    }

    public IModelSettings? CurrentModelSettingsModule
    {
        get => _modelSettingsModule;
        private set => this.RaiseAndSetIfChanged(ref _modelSettingsModule, value);
    }

    public Bitmap? GeneratedPublicLinkQRCode
    {
        get => _generatedPublicLinkQRCode;
        set => this.RaiseAndSetIfChanged(ref _generatedPublicLinkQRCode, value);
    }

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> AddNewApiKeyCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> RemoveApiKeyCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> EnsurePythonEnvironmentCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> ConfirmLoginCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> SelectModuleCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> ValidateApiKeyCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> GenerateLinkCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> RunMulticallerCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> StopGradioServerCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> DownloadAllFilesCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> DownloadSelectedAsPdfCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> BackToModelSettingsCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> CopyLastTenMessagesCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> CopyAllMessagesCommand { get; private set; }
    public ReactiveCommand<Unit, bool> ToggleShowTimestampCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> AddFolderCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> RemoveItemCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> RenameItemCommand { get; private set; }

    #endregion

    #region Initialization

    private void InitializeCommands()
    {
        AddNewApiKeyCommand = ReactiveCommand.CreateFromTask(AddNewApiKeyAsync);
        RemoveApiKeyCommand = ReactiveCommand.Create(RemoveSelectedApiKey, this.WhenAnyValue(x => x.IsApiKeySelected));
        EnsurePythonEnvironmentCommand = ReactiveCommand.CreateFromTask(EnsurePythonEnvironmentAsync);
        ConfirmLoginCommand = ReactiveCommand.CreateFromTask(ConfirmLoginAsync, 
            this.WhenAnyValue(x => x.SelectedApiKey).Select(apiKey => apiKey != null));
        SelectModuleCommand = ReactiveCommand.CreateFromTask(SelectModuleAsync, 
            this.WhenAnyValue(x => x.SelectedModelType).Select(mt => !string.IsNullOrEmpty(mt)));
        ValidateApiKeyCommand = ReactiveCommand.CreateFromTask(ValidateApiKeyAsync);
        GenerateLinkCommand = ReactiveCommand.CreateFromTask(GenerateLinkAsync);
        RunMulticallerCommand = ReactiveCommand.CreateFromTask(RunMulticallerAsync);
        StopGradioServerCommand = ReactiveCommand.CreateFromTask(StopGradioServerAsync);
        DownloadAllFilesCommand = ReactiveCommand.CreateFromTask(DownloadAllFilesAsync);
        DownloadSelectedAsPdfCommand = ReactiveCommand.CreateFromTask(DownloadSelectedAsPdfAsync);
        BackToModelSettingsCommand = ReactiveCommand.CreateFromTask(BackToModelSettingsAsync);
        CopyLastTenMessagesCommand = ReactiveCommand.CreateFromTask(CopyLastTenMessagesAsync);
        CopyAllMessagesCommand = ReactiveCommand.CreateFromTask(CopyAllMessagesAsync);
        ToggleShowTimestampCommand = ReactiveCommand.Create(() => ShowTimestamp = !ShowTimestamp);
        AddFolderCommand = ReactiveCommand.CreateFromTask(AddFolderAsync);
        RemoveItemCommand = ReactiveCommand.Create(RemoveItem);
        RenameItemCommand = ReactiveCommand.CreateFromTask(RenameItemAsync);
    }

    #endregion

    #region Event Handlers

    private void OnConsoleMessageOccurred(object? sender, string message)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var consoleMessage = ConsoleMessageManager.CreateConsoleMessage(message, MessageType.Info);
            ConsoleMessages.Add(consoleMessage);
            SelectedConsoleIndex = ConsoleMessages.Count - 1;
        });
    }

    private void OnExceptionOccurred(object? sender, string message)
    {
        var consoleMessage = ConsoleMessageManager.CreateConsoleMessage(message, MessageType.Error);
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            ConsoleMessages.Add(consoleMessage);
            SelectedConsoleIndex = ConsoleMessages.Count - 1;
        });
    }

    #endregion

    #region Methods - Python / Module
    
    private async Task<Unit> SelectModuleAsync()
    {
        // For now, simply logged (unused?)
        ConsoleMessageManager.LogInfo($"<HERE IT IS CALLED: > Module selected: {SelectedModelType}");
        return Unit.Default;
    }


    private async Task<Unit> AddNewApiKeyAsync()
    {
        try
        {
            await _apiKeyManager.AddNewAPIKeyAsync(_dialogService.PromptUserAsync);
            ConsoleMessageManager.LogInfo("New API Key added successfully.");
        }
        catch (Exception ex)
        {
            AddExceptionMessageToConsole(ex);
        }
        return Unit.Default;
    }

    private void RemoveSelectedApiKey()
    {
        try
        {
            _apiKeyManager.RemoveAPIKey(SelectedApiKey);
            SelectedApiKey = null;
            ConsoleMessageManager.LogInfo("Selected API Key removed successfully.");
        }
        catch (Exception ex)
        {
            AddExceptionMessageToConsole(ex);
        }
    }

    private async Task<Unit> EnsurePythonEnvironmentAsync()
    {
        try
        {
            IsBusy = true;
            await _pythonEnvironmentInitializer.InitializePythonEnvironmentAsync();
            PythonPath = _pythonEnvironmentInitializer.GetPythonPath();
            IsPythonPathLocked = true;
            ConsoleMessageManager.LogInfo($"Python environment initialized at path: {PythonPath}");
        }
        catch (Exception ex)
        {
            AddExceptionMessageToConsole(ex);
        }
        finally
        {
            IsBusy = false;
        }
        return Unit.Default;
    }

    private async Task<Unit> ConfirmLoginAsync()
    {
        try
        {
            IsBusy = true;

            if (SelectedApiKey == null)
            {
                throw new ArgumentException("Please select or add an API key.");
            }

            ApiKey = SelectedApiKey.Key;
            _pythonInitSuccess = await InstantiateModuleWithInstanceOfIAPIHandlerAsync();

            if (_pythonInitSuccess)
            {
                await ValidateApiKeyAsync();
                SavePythonPath();
            }
            else
            {
                ConsoleMessageManager.LogError("Python initialization failed. Please check the Python path and try again.");
            }

            if (SavedApiKeys.Count == 0)
            {
                throw new ArgumentException("Saved API keys are empty.");
            }

            _apiKeyManager.UpdateLastUsedAPIKey(SelectedApiKey);
            ConsoleMessageManager.LogInfo("Last used API Key updated successfully.");
        }
        catch (Exception ex)
        {
            AddExceptionMessageToConsole(ex);
        }
        finally
        {
            IsBusy = false;
        }
        return Unit.Default;
    }

    private async Task<bool> InstantiateModuleWithInstanceOfIAPIHandlerAsync()
    {
        if (_pythonService != null)
        {
            Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogInfo("PythonExecutionService is already initialized."));
            return _pythonRunning;
        }

        try
        {
            Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogInfo("Initializing PythonExecutionService..."));

            if (string.IsNullOrEmpty(PythonPath))
            {
                throw new Exception("Python initialization failed. Python path is null.");
            }

            _pythonService = PythonExecutionService.GetInstance(PythonPath);
            Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogInfo("PythonExecutionService instantiated successfully."));

            _pythonService.ExceptionOccurred += (_, errorMessage) =>
            {
                var exception = new Exception($"<PES> Error: {errorMessage}");
                AddExceptionMessageToConsole(exception);
            };

            _pythonService.ConsoleMessageOccurred += (_, message) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var color = DetermineColorForPythonMessage(message);
                    var messageType = DetermineMessageType(message, color);
                    var consoleMessage = ConsoleMessageManager.CreateConsoleMessage(message, messageType);
                    AddToConsole(consoleMessage);
                });
            };

            var initSuccess = await _pythonService.InitializationTask;

            if (!initSuccess)
            {
                Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogError("PythonExecutionService failed to initialize."));
                _pythonService = null;
                IsPythonPathLocked = false;
                return false;
            }

            // Now create the appropriate IAPIHandler and store it in _apiService
            IAPIHandler? apiHandler = InitializeApiHandler();
            if (_apiService == null)
            {
                var ex = new ArgumentNullException("APIService is not initialized; cannot proceed.");
                AddExceptionMessageToConsole(ex);
                _pythonRunning = false;
                return false;
            }

            // Listen to the new API service
            _apiService.ConsoleMessageOccured += (_, args) =>
            {
                Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogInfo("<APIS> " + args));
            };
            _apiService.ErrorMessageOccured += (_, args) =>
            {
                Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogError("<APIS error> " + args));
            };

            Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogInfo("PythonExecutionService is up and running."));
            _pythonRunning = true;
            IsPythonPathLocked = true;
            return true;
        }
        catch (Exception e)
        {
            AddExceptionMessageToConsole(e);
            _pythonRunning = false;
            _pythonService = null;
            IsPythonPathLocked = false;
            return false;
        }
        finally
        {
            Dispatcher.UIThread.InvokeAsync(() => { IsBusy = false; });
        }
    }

    private IAPIHandler? InitializeApiHandler()
    {
        switch (SelectedModelType)
        {
            case "OpenAI":
                CurrentModelSettingsModule = new OpenAI_v2_ModelSettings();
                var openAIHandler = new OpenAI_v2_APIHandler(_pythonService);
                _apiService = new APIService(openAIHandler);
                Navigation.NavigateToModelSettings();
                return openAIHandler;

            case "OpenAI o1-line":
                CurrentModelSettingsModule = new OpenAI_o1_ModelSettings();
                var openAI_o1Handler = new OpenAI_o1_APIHandler(_pythonService);
                _apiService = new APIService(openAI_o1Handler);
                Navigation.NavigateToModelSettings();
                return openAI_o1Handler;

            case "Hugging Face Serverless Inference":
                CurrentModelSettingsModule = new HFServerlessInferenceModelSettings();
                var hfHandler = new HFServerlessInference_APIHandler(_pythonService);
                _apiService = new APIService(hfHandler);
                Navigation.NavigateToModelSettings();
                return hfHandler;

            case "OpenAI Multicaller":
                CurrentModelSettingsModule = new OpenAI_Multicaller_ModelSettings();
                var multicallerHandler = new OpenAI_Multicaller_APIHandler(_pythonService);
                _apiService = new APIService(multicallerHandler);
                // For multicaller, navigate to a special settings screen
                Navigation.NavigateToMulticallerModelSettings();
                return multicallerHandler;

            default:
                return null;
        }
    }

    private SolidColorBrush DetermineColorForPythonMessage(string message)
    {
        if (message.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("exception", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(Colors.DarkRed);
        }
        if (message.Contains("successfully", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(Colors.ForestGreen);
        }
        return new SolidColorBrush(Colors.DarkSalmon);
    }

    private MessageType DetermineMessageType(string message, SolidColorBrush color)
    {
        if (color.Color == Colors.DarkRed)
            return MessageType.Error;
        if (color.Color == Colors.ForestGreen)
            return MessageType.Info;
        return MessageType.Info;
    }

    private async Task<Unit> ValidateApiKeyAsync()
    {
        ConsoleMessageManager.PrintNetworkWarning();
        if (!_pythonRunning)
            return Unit.Default;

        try
        {
            if (_apiService == null)
                throw new Exception("Validation failed: _apiService is uninitialized.");

            ConsoleMessageManager.LogInfo("Validating API Key...");
            IsBusy = true;

            if (string.IsNullOrEmpty(ApiKey))
            {
                throw new ArgumentException("No API key / token found. Please enter one.");
            }

            bool validApiKey = await _apiService.ValidateApiKeyAsync(ApiKey);

            if (validApiKey)
            {
                ConsoleMessageManager.LogInfo("Validated API key / token successfully.");

                if (CurrentModelSettingsModule == null)
                    throw new NullReferenceException("CurrentModelSettingsModule is null after validation.");

                // If not multicaller, navigate to normal model settings
                if (CurrentModelSettingsModule.GetType() != typeof(OpenAI_Multicaller_ModelSettings))
                {
                    Navigation.NavigateToModelSettings();
                }
                else
                {
                    Navigation.NavigateToMulticallerModelSettings();
                }

                var models = await _apiService.GetAvailableModelsAsync(ApiKey);
                CurrentModelSettingsModule.AvailableModels.Clear();
                foreach (string model in models)
                {
                    CurrentModelSettingsModule.AvailableModels.Add(model);
                }

                CurrentModelSettingsModule.SelectedModel =
                    CurrentModelSettingsModule.AvailableModels.FirstOrDefault();
            }
            else
            {
                throw new ArgumentException("Could not verify the entered API key / token. Check credentials.");
            }
        }
        catch (Exception ex)
        {
            ConsoleMessageManager.LogError($"Validation failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
        return Unit.Default;
    }

    private void SavePythonPath()
    {
        try
        {
            var pythonPathFile = PathManager.Combine(PathManager.GetBaseDirectory(), "python_path.txt");
            File.WriteAllText(pythonPathFile, PythonPath ?? "");
            ConsoleMessageManager.LogInfo($"Python path saved to {pythonPathFile}.");
        }
        catch (Exception ex)
        {
            AddExceptionMessageToConsole(ex);
        }
    }

    #endregion

    #region Methods - Link Generation / Multicaller

    private async Task<Unit> GenerateLinkAsync()
    {
        ConsoleMessageManager.PrintNetworkWarning();
        try
        {
            if (string.IsNullOrEmpty(ApiKey))
                throw new ArgumentException("Please enter a valid API key / token.");

            IsBusy = true;
            if (_apiService == null)
                throw new NullReferenceException("API Service is null.");

            var (localLink, publicLink) = await _apiService.StartGradioInterfaceAsync(ApiKey, CurrentModelSettingsModule);

            ConsoleMessageManager.LogInfo($"Local Link: {localLink}, Public Link: {publicLink}");

            CurrentModelSettingsModule!.GeneratedLocalLink = localLink;
            CurrentModelSettingsModule.GeneratedPublicLink = publicLink;

            // QR Code 
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(publicLink, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeAsPng = qrCode.GetGraphic(20);

                using (var stream = new MemoryStream(qrCodeAsPng))
                {
                    GeneratedPublicLinkQRCode = new Bitmap(stream);
                }
            }

            IsServerRunning = true;
            Navigation.NavigateToLinkGeneration();
        }
        catch (Exception e)
        {
            AddExceptionMessageToConsole(e);
        }
        finally
        {
            IsBusy = false;
        }
        return Unit.Default;
    }

    private async Task<Unit> RunMulticallerAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(ApiKey))
                throw new ArgumentException("Please enter a valid API key / token.");

            if (_apiService == null)
                throw new NullReferenceException("API Service is null.");

            ConsoleMessageManager.LogInfo("Multicaller started.");
            IsServerRunning = true;

            // Switch to data collection once it’s done
            var endMessage = await _apiService.RunMulticallerAsync(ApiKey, CurrentModelSettingsModule);
            ConsoleMessageManager.LogInfo($"Multicaller ended with message: {endMessage}.");

            IsServerRunning = false;
            LoadChatHistories();
            Navigation.NavigateToDataCollection();
        }
        catch (Exception e)
        {
            AddExceptionMessageToConsole(e);
        }
        finally
        {
            IsBusy = false;
        }
        return Unit.Default;
    }

    private async Task<Unit> StopGradioServerAsync()
    {
        try
        {
            ConsoleMessageManager.LogInfo("Stopping server...");
            if (_apiService == null)
                throw new NullReferenceException("API Service is null.");

            var stopMessage = await _apiService.StopGradioInterfaceAsync();
            ConsoleMessageManager.LogInfo($"Server stopped with message: {stopMessage}");

            IsServerRunning = false;
            LoadChatHistories();

            // Possibly navigate to DataCollection or ModelSettings, etc.
            Navigation.NavigateToDataCollection();
        }
        catch (Exception e)
        {
            AddExceptionMessageToConsole(e);
        }
        return Unit.Default;
    }

    private async Task<Unit> BackToModelSettingsAsync()
    {
        await StopGradioServerAsync();
        IsServerRunning = false;
        Navigation.NavigateToModelSettings();
        return Unit.Default;
    }

    #endregion

    #region Methods - Data Collection / Chat History

    private void LoadChatHistories()
    {
        var directoryPath = PathManager.Combine(PathManager.GetBaseDirectory(), "Scripts", "chat_histories");
        ChatHistoryCollection.LoadFiles(directoryPath);
    }

    private async Task<Unit> DownloadAllFilesAsync()
    {
        try
        {
            var topLevel = GetTopLevel();
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose a folder to copy all chat histories to."
            });

            if (folders.Count > 0)
            {
                var targetDirectory = folders[0].Path.LocalPath;
                var sourceDirectory = PathManager.Combine(PathManager.GetBaseDirectory(), "Scripts", "chat_histories");

                foreach (var file in Directory.GetFiles(sourceDirectory, "*.json"))
                {
                    var destFile = Path.Combine(targetDirectory, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                    ConsoleMessageManager.LogInfo($"Copied {file} to {destFile}.");
                }

                await _dialogService.ShowMessageAsync("Download successful", "All JSON-files were successfully downloaded.");
            }
            else
            {
                ConsoleMessageManager.LogWarning("No directory selected.");
            }
        }
        catch (Exception ex)
        {
            ConsoleMessageManager.LogError($"Error: {ex.Message}");
            await _dialogService.ShowMessageAsync("Download was not successful", $"There was an error: {ex.Message}");
        }
        return Unit.Default;
    }

    private async Task<Unit> DownloadSelectedAsPdfAsync()
    {
        try
        {
            if (ChatHistoryCollection.SelectedFile == null || string.IsNullOrEmpty(ChatHistoryCollection.SelectedFile.Filename))
            {
                await _dialogService.ShowMessageAsync("No file chosen", "Please select a chat history to download.");
                ConsoleMessageManager.LogWarning("No chat history selected.");
                return Unit.Default;
            }

            var topLevel = GetTopLevel();
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export the chosen chat history as a PDF file.",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType("PDF Files") { Patterns = new[] { "*.pdf" } }
                },
                SuggestedFileName = $"{Path.GetFileNameWithoutExtension(ChatHistoryCollection.SelectedFile.Filename)}.pdf"
            });

            if (file is not null)
            {
                var pdfPath = file.Path.LocalPath;
                ConsoleMessageManager.LogInfo($"PDF is saved under {pdfPath}.");
                GeneratePdf(pdfPath);
                await _dialogService.ShowMessageAsync("Export successful", "The chosen chat history was successfully exported as PDF.");
            }
            else
            {
                ConsoleMessageManager.LogWarning("No file path selected for PDF export.");
            }
        }
        catch (Exception ex)
        {
            ConsoleMessageManager.LogError($"Error: {ex.Message}");
            await _dialogService.ShowMessageAsync("Download not successful", $"There was an error: {ex.Message}");
        }
        return Unit.Default;
    }

    private void GeneratePdf(string pdfPath)
    {
        try
        {
            var pdf = new ChatHistoryDocument(ChatHistoryCollection);
            pdf.GeneratePdf(pdfPath);
            ConsoleMessageManager.LogInfo($"PDF generated at {pdfPath}.");
        }
        catch (Exception ex)
        {
            AddExceptionMessageToConsole(ex);
        }
    }

    private void RemoveItem()
    {
        try
        {
            ChatHistoryCollection.RemoveItem(ChatHistoryCollection.SelectedFile);
            ConsoleMessageManager.LogInfo("Item removed successfully.");
        }
        catch (Exception ex)
        {
            AddExceptionMessageToConsole(ex);
        }
    }

    private async Task<Unit> AddFolderAsync()
    {
        var folderName = await _dialogService.PromptUserAsync("Enter the name of the new folder:");
        if (string.IsNullOrWhiteSpace(folderName))
        {
            var consoleMessage = ConsoleMessageManager.CreateConsoleMessage("Folder creation canceled by user.", MessageType.Info);
            AddToConsole(consoleMessage);
            return Unit.Default;
        }

        try
        {
            ChatHistoryCollection.AddFolder(folderName);
            var consoleMessage = ConsoleMessageManager.CreateConsoleMessage($"Folder '{folderName}' added successfully.", MessageType.Info);
            AddToConsole(consoleMessage);
        }
        catch (Exception ex)
        {
            AddExceptionMessageToConsole(ex);
        }

        return Unit.Default;
    }

    private async Task<Unit> RenameItemAsync()
    {
        var newName = await _dialogService.PromptUserAsync("Enter the new name:");
        if (string.IsNullOrWhiteSpace(newName))
        {
            var consoleMessage = ConsoleMessageManager.CreateConsoleMessage("Rename operation canceled by user.", MessageType.Info);
            AddToConsole(consoleMessage);
            return Unit.Default;
        }

        try
        {
            ChatHistoryCollection.RenameItem(ChatHistoryCollection.SelectedFile, newName);
            var consoleMsg = ConsoleMessageManager.CreateConsoleMessage("Item renamed successfully.", MessageType.Info);
            AddToConsole(consoleMsg);
        }
        catch (Exception ex)
        {
            AddExceptionMessageToConsole(ex);
        }

        return Unit.Default;
    }

    #endregion

    #region Methods - Console

    private void AddToConsole(ConsoleMessage message)
    {
        ConsoleMessages.Add(message);
        SelectedConsoleIndex = ConsoleMessages.Count - 1;
    }

    private void AddExceptionMessageToConsole(Exception exception)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var isPythonError = exception.Message.Contains("<PES stderr>", StringComparison.OrdinalIgnoreCase);
            var messageType = isPythonError ? MessageType.PythonStdErr : MessageType.Error;
            var msg = isPythonError
                ? exception.Message.Replace("<PES stderr>", "").Trim()
                : "<ShellVM error> " + exception.Message;

            var consoleMessage = ConsoleMessageManager.CreateConsoleMessage(msg, messageType);
            AddToConsole(consoleMessage);
        });
    }

    #endregion

    #region Methods - Clipboard

    private async Task<Unit> CopyLastTenMessagesAsync()
    {
        if (ConsoleMessages.Any())
        {
            var lastTen = ConsoleMessages.TakeLast(10);
            var textToCopy = string.Join(Environment.NewLine,
                lastTen.Select(msg => $"[{msg.Timestamp}] {msg.Text}"));

            var clipboard = Clipboard.Get();
            await clipboard.SetTextAsync(textToCopy);

            ConsoleMessageManager.LogInfo("Last ten messages copied to clipboard.");
        }
        else
        {
            ConsoleMessageManager.LogWarning("No messages to copy.");
        }
        return Unit.Default;
    }

    private async Task<Unit> CopyAllMessagesAsync()
    {
        var all = string.Join(Environment.NewLine,
            ConsoleMessages.Select(msg => $"[{msg.Timestamp}] {msg.Text}"));

        var clipboard = Clipboard.Get();
        await clipboard.SetTextAsync(all);

        ConsoleMessageManager.LogInfo("All messages copied to clipboard.");
        return Unit.Default;
    }

    #endregion

    #region Methods - Helpers

    private TopLevel GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            return desktop.MainWindow;
        }
        throw new InvalidOperationException("Unable to find the main window (top-level).");
    }

    public void Dispose()
    {
        ConsoleMessageManager.PrintGoodbyeMessage();
        _apiService?.Dispose();
        _pythonService?.Dispose();

        var listener = Trace.Listeners.OfType<InternalConsoleTraceListener>().FirstOrDefault();
        if (listener != null)
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    #endregion
}