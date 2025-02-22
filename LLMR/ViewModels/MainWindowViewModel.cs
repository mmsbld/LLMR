using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LLMR.Helpers;
using LLMR.Services;
using LLMR.Services.HFServerlessInference;
using LLMR.Services.OpenAI_Multicaller;
using LLMR.Services.OpenAI_v2;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ReactiveUI;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using LLMR.Model;
using LLMR.Model.ChatHistoryManager;
using LLMR.Model.ModelSettingModulesManager;
using LLMR.Model.ModelSettingModulesManager.ModelSettingsModules;
using LLMR.Services.OpenAI_o1;
using LLMR.Views;
using Unit = System.Reactive.Unit;

namespace LLMR.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        #region NEW CODE UI-REVAMP
        
        // NEW: holds the current main view (i.e. the content on the right)
        private object _currentMainView;
        public object CurrentMainView
        {
            get => _currentMainView;
            set => this.RaiseAndSetIfChanged(ref _currentMainView, value);
        }

        // NEW: returns the name of the current "main" tab (non-data collection)
        private string _currentNonDataCollectionTab;
        public string CurrentNonDataCollectionTab
        {
            get => _currentNonDataCollectionTab;
            set => this.RaiseAndSetIfChanged(ref _currentNonDataCollectionTab, value);
        }

        // NEW: Commands for switching views and opening windows
        public ReactiveCommand<Unit, Unit> SwitchToMainTabCommand { get; }
        public ReactiveCommand<Unit, Unit> SwitchToDataCollectionCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenAboutCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }

        #endregion
        
        #region Fields

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
        private bool _pythonRunning;
        private bool _pythonInitSuccess;

        private readonly PythonEnvironmentInitializer _pythonEnvironmentInitializer;
        private readonly APIKeyManager _apiKeyManager;
        private readonly DialogService _dialogService;
        private PythonExecutionService? _pythonService;

        public MainWindowViewManager ViewManager { get; }

        #endregion

        #region Properties

        public bool IsPythonPathLocked
        {
            get => _isPythonPathLocked;
            set => this.RaiseAndSetIfChanged(ref _isPythonPathLocked, value);
        }
        private bool _isPythonPathLocked;

        private string? ApiKey
        {
            get => _apiKey;
            set => this.RaiseAndSetIfChanged(ref _apiKey, value);
        }

        public string? PythonPath
        {
            get => _pythonPath;
            set => this.RaiseAndSetIfChanged(ref _pythonPath, value);
        }

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

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                this.RaiseAndSetIfChanged(ref _isBusy, value);
                if (!IsConsoleExpanded && value)
                    IsConsoleExpanded = true;
            }
        }

        public bool IsConsoleExpanded
        {
            get => _isConsoleExpanded;
            set
            {
                if (IsBusy)
                    this.RaiseAndSetIfChanged(ref _isConsoleExpanded, true);
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

        public Bitmap? GeneratedPublicLinkQRCode
        {
            get => _generatedPublicLinkQRCode;
            set => this.RaiseAndSetIfChanged(ref _generatedPublicLinkQRCode, value);
        }

        public IModelSettings? CurrentModelSettingsModule
        {
            get => _modelSettingsModule;
            private set => this.RaiseAndSetIfChanged(ref _modelSettingsModule, value);
        }

        public string? SelectedModelType
        {
            get => _selectedModelType;
            set => this.RaiseAndSetIfChanged(ref _selectedModelType, value);
        }

        public ObservableCollection<string> AvailableModuleTypes { get; set; }

        public ObservableCollection<ConsoleMessage> ConsoleMessages { get; } = [];

        public ChatHistoryCollection ChatHistoryCollection { get; }

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

        #endregion

        #region API Key Management

        public ObservableCollection<APIKeyEntry?> SavedApiKeys => _apiKeyManager.SavedApiKeys;

        private readonly ObservableAsPropertyHelper<bool> _isApiKeySelected;
        public bool IsApiKeySelected => _isApiKeySelected.Value;

        private APIKeyEntry? _selectedApiKey;
        public APIKeyEntry? SelectedApiKey
        {
            get => _selectedApiKey;
            set => this.RaiseAndSetIfChanged(ref _selectedApiKey, value);
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

        #region Constructor

        public MainWindowViewModel()
        {
            _pythonRunning = false;

            _apiKeyManager = new APIKeyManager();
            _dialogService = new DialogService();
            _pythonEnvironmentInitializer = new PythonEnvironmentInitializer();

            AvailableModuleTypes = ["OpenAI", "OpenAI o1-line", "Hugging Face Serverless Inference", "OpenAI Multicaller"];
            SelectedModelType = "OpenAI"; // Default selection

            ViewManager = new MainWindowViewManager();

            ServerStatus = "Stopped";
            ServerStatusColor = Brushes.Red;

            ChatHistoryCollection = new ChatHistoryCollection();
            ChatHistoryCollection.ConsoleMessageOccurred += OnConsoleMessageOccurred;
            ChatHistoryCollection.ExceptionOccurred += OnExceptionOccurred;
            
            LoadChatHistories();

            _isApiKeySelected = this.WhenAnyValue(x => x.SelectedApiKey)
                .Select(apiKey => apiKey != null)
                .ToProperty(this, x => x.IsApiKeySelected);

            InitializeCommands();
            SubscribeToConsoleMessageManager();

            IsBusy = false;

            SelectedApiKey = SavedApiKeys.FirstOrDefault(k => k != null && k.IsLastUsed) ?? SavedApiKeys.FirstOrDefault();

            //ToDo: Make sure that we are allowed to use this license type! (+move to xml typed settings?)
            QuestPDF.Settings.License = LicenseType.Community;

            #region NEWCODE UI-REVAMP
            
            CurrentMainView = new LLMR.Views.Tabs.LoginTab();
            CurrentNonDataCollectionTab = "Login";
            
            SwitchToMainTabCommand = ReactiveCommand.Create(() =>
            {
                CurrentMainView = new LLMR.Views.Tabs.LoginTab();
                CurrentNonDataCollectionTab = "Login";
            });

            SwitchToDataCollectionCommand = ReactiveCommand.Create(() =>
            {
                // Switch to the Data Collection view
                CurrentMainView = new LLMR.Views.Tabs.DataCollectionTab();
                LoadChatHistories();
            });

            OpenAboutCommand = ReactiveCommand.Create(() =>
            {
                // Create and open the About window
                var aboutWindow = new AboutWindow();
                // Optionally, set the owner to your main window if you have access to it:
                aboutWindow.ShowDialog(App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
            });

            OpenSettingsCommand = ReactiveCommand.Create(() =>
            {
                // Create and open the Settings window
                var settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog(App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
            });

            #endregion

            ViewManager.SwitchToLogin();

            Trace.Listeners.Add(new InternalConsoleTraceListener(message =>
            {
                var consoleMessage = ConsoleMessageManager.CreateConsoleMessage(message, MessageType.Debug);
                AddToConsole(consoleMessage);
            }));
            
            ConsoleMessageManager.PrintSystemInfo();
            ConsoleMessageManager.PrintNetworkWarning();
            ConsoleMessageManager.PrintWelcomeMessage();
        }

        #endregion

        #region Methods

        private void InitializeCommands()
        {
            AddNewApiKeyCommand = ReactiveCommand.CreateFromTask(AddNewApiKeyAsync);
            RemoveApiKeyCommand = ReactiveCommand.Create(RemoveSelectedApiKey, this.WhenAnyValue(x => x.IsApiKeySelected));
            EnsurePythonEnvironmentCommand = ReactiveCommand.CreateFromTask(EnsurePythonEnvironmentAsync);
            ConfirmLoginCommand = ReactiveCommand.CreateFromTask(ConfirmLoginAsync, this.WhenAnyValue(x => x.SelectedApiKey).Select(apiKey => apiKey != null));
            SelectModuleCommand = ReactiveCommand.CreateFromTask(SelectModuleAsync, this.WhenAnyValue(x => x.SelectedModelType).Select(modelType => !string.IsNullOrEmpty(modelType)));
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

        private void SubscribeToConsoleMessageManager()
        {
            ConsoleMessageManager.OnConsoleMessageCreated += message =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ConsoleMessages.Add(message);
                    SelectedConsoleIndex = ConsoleMessages.Count - 1;
                });
            };
        }

        // Corrected event handler signatures
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

        private async Task<Unit> AddNewApiKeyAsync()
        {
            try
            {
                // Pass the delegate _dialogService.PromptUserAsync directly
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
                    throw new ArgumentException("Saved API keys are empty.");

                _apiKeyManager.UpdateLastUsedAPIKey(SelectedApiKey);
                ConsoleMessageManager.LogInfo("Last used API Key updated successfully.");
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

        private async Task<bool> InstantiateModuleWithInstanceOfIAPIHandlerAsync()
        {
            if (_pythonService != null)
            {
                Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogInfo("PES is already initialized."));
                return _pythonRunning;
            }

            try
            {
                Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogInfo("Initializing PES..."));

                if (string.IsNullOrEmpty(PythonPath))
                    throw new Exception("Python initialization failed. Python path is null.");

                _pythonService = PythonExecutionService.GetInstance(PythonPath);
                Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogInfo("PES instantiated successfully."));

                _pythonService.ExceptionOccurred += (_, errorMessage) =>
                {
                    var exception = new Exception($"<PES> Error: {errorMessage}");
                    AddExceptionMessageToConsole(exception);
                };

                _pythonService.ConsoleMessageOccurred += (_, message) =>
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        SolidColorBrush color = DetermineColorForPythonMessage(message);
                        MessageType messageType = DetermineMessageType(message, color);
                        var consoleMessage = ConsoleMessageManager.CreateConsoleMessage(message, messageType);
                        AddToConsole(consoleMessage);
                    });
                };

                var initSuccess = await _pythonService.InitializationTask;

                if (!initSuccess)
                {
                    Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogError("PES failed to initialize."));
                    _pythonService = null;
                    IsPythonPathLocked = false;
                    return false;
                }

                IAPIHandler? apiHandler = InitializeApiHandler();

                if (_apiService == null)
                {
                    var exception = new ArgumentNullException("PES is uninitializable, since APIS is not initialized.");
                    AddExceptionMessageToConsole(exception);
                    _pythonRunning = false;
                    return false;
                }

                _apiService.ConsoleMessageOccured += (_, args) =>
                {
                    Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogInfo("<APIS> " + args));
                };

                _apiService.ErrorMessageOccured += (_, args) =>
                {
                    Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogError("<APIS error> " + args));
                };

                Dispatcher.UIThread.InvokeAsync(() => ConsoleMessageManager.LogInfo("PES initialized and running."));
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
            IAPIHandler? apiHandler = null;
            switch (SelectedModelType)
            {
                case "OpenAI":
                    CurrentModelSettingsModule = new OpenAI_v2_ModelSettings();
                    apiHandler = new OpenAI_v2_APIHandler(_pythonService);
                    _apiService = new APIService(apiHandler);
                    ViewManager.GradioMode = true;
                    break;
                case "OpenAI o1-line":
                    CurrentModelSettingsModule = new OpenAI_o1_ModelSettings();
                    apiHandler = new OpenAI_o1_APIHandler(_pythonService);
                    _apiService = new APIService(apiHandler);
                    ViewManager.GradioMode = true;
                    break;
                case "Hugging Face Serverless Inference":
                    CurrentModelSettingsModule = new HFServerlessInferenceModelSettings();
                    apiHandler = new HFServerlessInference_APIHandler(_pythonService);
                    _apiService = new APIService(apiHandler);
                    ViewManager.GradioMode = true;
                    break;
                case "OpenAI Multicaller":
                    CurrentModelSettingsModule = new OpenAI_Multicaller_ModelSettings();
                    apiHandler = new OpenAI_Multicaller_APIHandler(_pythonService);
                    _apiService = new APIService(apiHandler);
                    ViewManager.MulticallerMode = true;
                    break;
            }

            return apiHandler;
        }

        private SolidColorBrush DetermineColorForPythonMessage(string message)
        {
            if (message.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("exception", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Colors.DarkRed);
            }
            else if (message.Contains("successfully", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Colors.ForestGreen);
            }
            else
            {
                return new SolidColorBrush(Colors.DarkSalmon);
            }
        }

        private MessageType DetermineMessageType(string message, SolidColorBrush color)
        {
            if (color.Color == Colors.DarkRed)
                return MessageType.Error;
            if (color.Color == Colors.ForestGreen)
                return MessageType.Info;
            if (color.Color == Colors.DarkSalmon)
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
                {
                    throw new Exception("Validation failed: _apiService is uninitialized but _pythonRunning is true.");
                }

                ConsoleMessageManager.LogInfo("Validating API Key ...");
                IsBusy = true;

                if (string.IsNullOrEmpty(ApiKey))
                {
                    throw new ArgumentException("Could not find an API key / token. Did you enter one?");
                }

                bool validApiKey = await _apiService.ValidateApiKeyAsync(ApiKey);

                if (validApiKey)
                {
                    ConsoleMessageManager.LogInfo("Validated API key / token successfully.");

                    if (CurrentModelSettingsModule == null)
                        throw new NullReferenceException("CurrentModelSettingsModule is null.");

                    if (CurrentModelSettingsModule.GetType() != typeof(OpenAI_Multicaller_ModelSettings)) 
                    {
                        ViewManager.SwitchToModelSettings();
                        CurrentMainView = new LLMR.Views.Tabs.ModelSettingsTab();
                        CurrentNonDataCollectionTab = "Model Settings";
                    }
                    else
                    {
                        ViewManager.SwitchToMulticallerModelSettings();
                        CurrentMainView = new LLMR.Views.Tabs.MulticallerTab();
                        CurrentNonDataCollectionTab = "Multicaller Settings";
                    }
                    var models = await _apiService.GetAvailableModelsAsync(ApiKey);

                    CurrentModelSettingsModule.AvailableModels.Clear();
                    foreach (string model in models)
                    {
                        CurrentModelSettingsModule.AvailableModels.Add(model);
                    }

                    CurrentModelSettingsModule.SelectedModel = CurrentModelSettingsModule.AvailableModels.FirstOrDefault();
                }
                else
                {
                    throw new ArgumentException("Could not verify the entered API key / token. Please validate your credentials and try again.");
                }
            }
            catch (InvalidOperationException ex)
            {
                ConsoleMessageManager.LogError($"Validation failed due to an error in the API service: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                ConsoleMessageManager.LogError(ex.Message);
            }
            catch (Exception ex)
            {
                ConsoleMessageManager.LogError($"An unexpected error occurred: {ex.Message}");
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
                System.IO.File.WriteAllText(pythonPathFile, PythonPath ?? "");
                ConsoleMessageManager.LogInfo($"Python path saved to {pythonPathFile}.");
            }
            catch (Exception ex)
            {
                AddExceptionMessageToConsole(ex);
            }
        }

        private async Task<Unit> GenerateLinkAsync()
        {
            ConsoleMessageManager.PrintNetworkWarning();
            try
            {
                if (string.IsNullOrEmpty(ApiKey))
                {
                    throw new ArgumentException("Please enter a valid API key / token.");
                }

                IsBusy = true;

                if (_apiService == null)
                    throw new NullReferenceException("API Service is null.");

                var (localLink, publicLink) = await _apiService.StartGradioInterfaceAsync(ApiKey, CurrentModelSettingsModule);

                ConsoleMessageManager.LogInfo($"Local Link: {localLink}, Public Link: {publicLink}");

                CurrentModelSettingsModule!.GeneratedLocalLink = localLink;
                CurrentModelSettingsModule.GeneratedPublicLink = publicLink;

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

                ViewManager.SwitchToLinkGeneration();
                ViewManager.IsModelSettingsEnabled = false;
                ViewManager.IsLinkGenerationEnabled = true;
                ViewManager.IsDataCollectionEnabled = true;
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
                {
                    throw new ArgumentException("Please enter a valid API key / token.");
                }

                if (_apiService == null)
                    throw new NullReferenceException("API Service is null.");

                // if (_apiService.GetType() != typeof(OpenAI_Multicaller_APIService))
                //     throw new NotSupportedException("<MWVM> API Service type is not supported.");
                //
                // var apiService = (OpenAI_Multicaller_APIService)_apiService;

                ConsoleMessageManager.LogInfo("Multicaller started.");
                IsServerRunning = true;
                ServerStatus = "Multicaller running";
                ServerStatusColor = Brushes.LimeGreen;

                ViewManager.SwitchToDataCollection();
                ViewManager.IsLoginEnabled = false;
                ViewManager.IsModelSettingsEnabled = false;
                ViewManager.IsMulticallerModelSettingsEnabled = false;
                ViewManager.IsLinkGenerationEnabled = false;
                ViewManager.IsDataCollectionEnabled = true;

                var endMessage = await _apiService.RunMulticallerAsync(ApiKey, CurrentModelSettingsModule);
                ConsoleMessageManager.LogInfo($"Multicaller ended with message: {endMessage}.");
                //ToDo: (Moe) Couldn't this lead to issues? Consider the setter logic for "IsServerRunning"!
                IsServerRunning = false;
                ServerStatus = "Multicaller stopped";
                ServerStatusColor = Brushes.Red;
                LoadChatHistories();

                ViewManager.IsLoginEnabled = false;
                ViewManager.IsModelSettingsEnabled = false;
                ViewManager.IsMulticallerModelSettingsEnabled = true;
                ViewManager.IsLinkGenerationEnabled = false;
                ViewManager.IsDataCollectionEnabled = true;
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

        private async Task<Unit> BackToModelSettingsAsync()
        {
            await StopGradioServerAsync();
            IsServerRunning = false;
            ViewManager.SwitchToModelSettings();
            return Unit.Default;
        }

        private async Task StopGradioServerAsync()
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
                ViewManager.SwitchToDataCollection();
                ViewManager.IsLinkGenerationEnabled = false;
            }
            catch (Exception e)
            {
                AddExceptionMessageToConsole(e);
            }
        }
        
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

                    foreach (var file in System.IO.Directory.GetFiles(sourceDirectory, "*.json"))
                    {
                        var destFile = System.IO.Path.Combine(targetDirectory, System.IO.Path.GetFileName(file));
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
                        new FilePickerFileType("PDF Files") { Patterns = ["*.pdf"] }
                    },
                    SuggestedFileName = $"{System.IO.Path.GetFileNameWithoutExtension(ChatHistoryCollection.SelectedFile.Filename)}.pdf"
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
                    ConsoleMessageManager.LogWarning("No chat history selected.");
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

        private async Task<Unit> CopyLastTenMessagesAsync()
        {
            if (ConsoleMessages.Any())
            {
                var lastTenMessages = ConsoleMessages.TakeLast(10);
                var textToCopy = string.Join(Environment.NewLine, lastTenMessages.Select(msg => $"[{msg.Timestamp}] {msg.Text}"));
        
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
            var allMessages = string.Join(Environment.NewLine, ConsoleMessages.Select(msg => $"[{msg.Timestamp}] {msg.Text}"));
            var clipboard = Clipboard.Get();
            await clipboard.SetTextAsync(allMessages);
            ConsoleMessageManager.LogInfo("All messages copied to clipboard.");

            return Unit.Default;
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
                var consoleMessage = ConsoleMessageManager.CreateConsoleMessage("Folder creation canceled by the user.", MessageType.Info);
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
                var consoleMessage = ConsoleMessageManager.CreateConsoleMessage("Rename operation canceled by the user.", MessageType.Info);
                AddToConsole(consoleMessage);
                return Unit.Default;
            }

            try
            {
                ChatHistoryCollection.RenameItem(ChatHistoryCollection.SelectedFile, newName);
                var consoleMessage = ConsoleMessageManager.CreateConsoleMessage("Item renamed successfully.", MessageType.Info);
                AddToConsole(consoleMessage);
            }
            catch (Exception ex)
            {
                AddExceptionMessageToConsole(ex);
            }

            return Unit.Default;
        }

        private void AddToConsole(ConsoleMessage message)
        {
            ConsoleMessages.Add(message);
            SelectedConsoleIndex = ConsoleMessages.Count - 1;
        }

        private void AddExceptionMessageToConsole(Exception exception)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var messageType = exception.Message.Contains("<PES stderr>") ? MessageType.PythonStdErr : MessageType.Error;
                var message = exception.Message.Contains("<PES stderr>") ?
                    exception.Message.Replace("<PES stderr>", "").Trim() :
                    "<MWVM error> " + exception.Message;
                var consoleMessage = ConsoleMessageManager.CreateConsoleMessage(message, messageType);
                AddToConsole(consoleMessage);
            });
        }


        private TopLevel GetTopLevel()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: not null } desktop) return desktop.MainWindow;
            throw new InvalidOperationException("<MWVM> Unable to get the main window.");
        }

        #endregion
        
        #region ModuleSelection

        private async Task<Unit> SelectModuleAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(SelectedModelType))
                {
                    throw new ArgumentException("Selected model type is empty.");
                }

                ConsoleMessageManager.LogInfo($"Selected model type: {SelectedModelType}");
                await InstantiateModuleWithInstanceOfIAPIHandlerAsync();
                ConsoleMessageManager.LogInfo($"Module '{SelectedModelType}' initialized successfully.");
            }
            catch (Exception ex)
            {
                AddExceptionMessageToConsole(ex);
            }

            return Unit.Default;
        }
        #endregion
        
        
        public void Dispose()
        {
            ConsoleMessageManager.PrintGoodbyeMessage();
            _apiService?.Dispose();
            _pythonService?.Dispose();

            var listener = Trace.Listeners.OfType<InternalConsoleTraceListener>().FirstOrDefault();
            if (listener == null) return;
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }
}
