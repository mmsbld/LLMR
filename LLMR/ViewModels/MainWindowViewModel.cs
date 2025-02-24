using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using LLMR.Helpers;
using LLMR.Services;
using LLMR.Services.HFServerlessInference;
using LLMR.Services.OpenAI_Multicaller;
using LLMR.Services.OpenAI_v2;
using QRCoder;
using QuestPDF.Infrastructure;
using ReactiveUI;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using LLMR.Model;
using LLMR.Model.ModelSettingModulesManager;
using LLMR.Model.ModelSettingModulesManager.ModelSettingsModules;
using LLMR.Services.OpenAI_o1;
using LLMR.Views;
using Unit = System.Reactive.Unit;

namespace LLMR.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    public DataCollectionManager DataCollectionManager { get; } 
    private bool _isLoginVisible;
    public bool IsLoginVisible
    {
        get => _isLoginVisible;
        set => this.RaiseAndSetIfChanged(ref _isLoginVisible, value);
    }

    private bool _isModelSettingsVisible;
    public bool IsModelSettingsVisible
    {
        get => _isModelSettingsVisible;
        set => this.RaiseAndSetIfChanged(ref _isModelSettingsVisible, value);
    }

    private bool _isMulticallerTabVisible;
    public bool IsMulticallerTabVisible
    {
        get => _isMulticallerTabVisible;
        set => this.RaiseAndSetIfChanged(ref _isMulticallerTabVisible, value);
    }

    private bool _isLinkGenerationVisible;
    public bool IsLinkGenerationVisible
    {
        get => _isLinkGenerationVisible;
        set => this.RaiseAndSetIfChanged(ref _isLinkGenerationVisible, value);
    }

    private bool _isDataCollectionVisible;
    public bool IsDataCollectionVisible
    {
        get => _isDataCollectionVisible;
        set => this.RaiseAndSetIfChanged(ref _isDataCollectionVisible, value);
    }

    // name of current non-data-collection tab (needed as btn label for tab switching)
    private string _currentNonDataCollectionTab;
    public string CurrentNonDataCollectionTab
    {
        get => _currentNonDataCollectionTab;
        set => this.RaiseAndSetIfChanged(ref _currentNonDataCollectionTab, value);
    }

    // commands for switching tabs (as user controls) and opening windows
    public ReactiveCommand<Unit, Unit> SwitchToMainTabCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToSecondaryTabCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenAboutCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }

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
    private SettingsModuleEntry? _selectedModelType;
    private IAPIService? _apiService;
    private bool _isServerRunning;
    private bool _pythonRunning;
    private bool _pythonInitSuccess;

    private readonly PythonEnvironmentInitializer _pythonEnvironmentInitializer;
    private readonly APIKeyManager _apiKeyManager;
    private readonly DialogService _dialogService;
    private PythonExecutionService? _pythonService;
        
    private DispatcherTimer _timerTime;
    private DispatcherTimer _timerDate;

    public MainWindowViewManager ViewManager { get; }
    
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

    public SettingsModuleEntry SelectedModelType
    {
        get => _selectedModelType;
        set => this.RaiseAndSetIfChanged(ref _selectedModelType, value);
    }

    public ObservableCollection<SettingsModuleEntry> AvailableModuleTypes { get; set; }

    public ObservableCollection<ConsoleMessage> ConsoleMessages { get; } = [];
    
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

    public ObservableCollection<APIKeyEntry?> SavedApiKeys => _apiKeyManager.SavedApiKeys;

    private readonly ObservableAsPropertyHelper<bool> _isApiKeySelected;
    public bool IsApiKeySelected => _isApiKeySelected.Value;

    private APIKeyEntry? _selectedApiKey;
    public APIKeyEntry? SelectedApiKey
    {
        get => _selectedApiKey;
        set => this.RaiseAndSetIfChanged(ref _selectedApiKey, value);
    }

    public ReactiveCommand<Unit, Unit> AddNewApiKeyCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> RemoveApiKeyCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> EnsurePythonEnvironmentCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> SetPythonPathToLastUsedPythonPathCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> ConfirmLoginCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> SelectModuleCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> ValidateApiKeyCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> GenerateLinkCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> RunMulticallerCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> StopGradioServerCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> BackToModelSettingsCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> CopyLastTenMessagesCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> CopyAllMessagesCommand { get; private set; }
    public ReactiveCommand<Unit, bool> ToggleShowTimestampCommand { get; private set; }
        
    public string CurrentTime => DateTime.Now.ToString("t", CultureInfo.CurrentCulture); // time without seconds
    public string CurrentDate => DateTime.Now.ToString("D", CultureInfo.CurrentCulture); // full date
    
    public MainWindowViewModel()
    {
        IsBusy = true;
        _pythonRunning = false;

        _apiKeyManager = new APIKeyManager();
        _dialogService = new DialogService();
        DataCollectionManager = new DataCollectionManager(_dialogService); 

        _pythonEnvironmentInitializer = new PythonEnvironmentInitializer();
        
        
        AvailableModuleTypes = new ObservableCollection<SettingsModuleEntry>
        {
            //ToDo: Add module entries and text etc. 
            new SettingsModuleEntry
            {
                IconPath = "avares://LLMR/Assets/icons/modules/oai.png",
                Title = "OpenAI",
                Description = "OpenAI v2 text here."
            },
            new SettingsModuleEntry
            {
                IconPath = "avares://LLMR/Assets/icons/modules/oai_o1.png",
                Title = "OpenAI o1-line",
                Description = "text TODO here."
            },
            new SettingsModuleEntry
            {
                IconPath = "avares://LLMR/Assets/icons/modules/hface_rd_smaller.png",
                Title = "Hugging Face Serverless Inference",
                Description = "Use serverless inference from Hugging Face text blabla here."
            },
            new SettingsModuleEntry
            {
                IconPath = "avares://LLMR/Assets/icons/modules/multicaller.png",
                Title = "OpenAI Multicaller",
                Description = "Multi-call aggregator for OpenAI requests."
            },
        };
        SelectedModelType = AvailableModuleTypes[0]; 

        ViewManager = new MainWindowViewManager();

        ServerStatus = "Stopped";
        ServerStatusColor = Brushes.Red;
        
        DataCollectionManager.LoadChatHistories();

        _isApiKeySelected = this.WhenAnyValue(x => x.SelectedApiKey)
            .Select(apiKey => apiKey != null)
            .ToProperty(this, x => x.IsApiKeySelected);

        InitializeCommands();
        SubscribeToConsoleMessageManager();

        SelectedApiKey = SavedApiKeys.FirstOrDefault(k => k != null && k.IsLastUsed) ?? SavedApiKeys.FirstOrDefault();

        //ToDo: Make sure that we are allowed to use this license type! (+move to xml typed settings?)
        QuestPDF.Settings.License = LicenseType.Community;

        // INIT VISIBILITY: start with Login
        IsLoginVisible = true;
        IsModelSettingsVisible = false;
        IsMulticallerTabVisible = false;
        IsLinkGenerationVisible = false;
        IsDataCollectionVisible = false;
        CurrentNonDataCollectionTab = "Login";

        // new view switching logic
        SwitchToMainTabCommand = ReactiveCommand.Create(() =>
        {
            ViewManager.SwitchToPrimaryTab();
        });

        SwitchToSecondaryTabCommand = ReactiveCommand.Create(() =>
        {
            ViewManager.SwitchToSecondaryTab();
                
        });

        OpenAboutCommand = ReactiveCommand.Create(() =>
        {
            var aboutWindow = new AboutWindow();
            // set owner to MainWindow (jan)
            aboutWindow.ShowDialog(App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
        });

        OpenSettingsCommand = ReactiveCommand.Create(() =>
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.ShowDialog(App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
        });
            

        ViewManager.SwitchToLogin();

        Trace.Listeners.Add(new InternalConsoleTraceListener(message =>
        {
            var consoleMessage = ConsoleMessageManager.CreateConsoleMessage(message, MessageType.Debug);
            AddToConsole(consoleMessage);
        }));
            
        ConsoleMessageManager.PrintSystemInfo();
        ConsoleMessageManager.PrintNetworkWarning();
        ConsoleMessageManager.PrintWelcomeMessage();
            
        InitializeTimerForCurrentTime();
        InitializeTimerForCurrentDate();

        IsBusy = false;
        IsConsoleExpanded = false;
    }
    
    private void InitializeTimerForCurrentTime()
    {
        _timerTime = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _timerTime.Tick += (s, e) =>
        {
            ((IReactiveObject)this).RaisePropertyChanged(new PropertyChangedEventArgs(nameof(CurrentTime)));
        };
        _timerTime.Start();
    }
    
    private void InitializeTimerForCurrentDate()
    {
        _timerDate = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
        _timerDate.Tick += (s, e) =>
        {
            ((IReactiveObject)this).RaisePropertyChanged(new PropertyChangedEventArgs(nameof(CurrentDate)));
        };
        _timerDate.Start();
    }

    private void InitializeCommands()
    {
        AddNewApiKeyCommand = ReactiveCommand.CreateFromTask(AddNewApiKeyAsync);
        RemoveApiKeyCommand = ReactiveCommand.Create(RemoveSelectedApiKey, this.WhenAnyValue(x => x.IsApiKeySelected));
        EnsurePythonEnvironmentCommand = ReactiveCommand.CreateFromTask(EnsurePythonEnvironmentAsync);
        SetPythonPathToLastUsedPythonPathCommand = ReactiveCommand.CreateFromTask(SetPythonPathToLastUsedPythonPathAsync);
        ConfirmLoginCommand = ReactiveCommand.CreateFromTask(ConfirmLoginAsync, this.WhenAnyValue(x => x.SelectedApiKey).Select(apiKey => apiKey != null));
        
        SelectModuleCommand = ReactiveCommand.CreateFromTask(
            SelectModuleAsync, 
            this.WhenAnyValue(x => x.SelectedModelType)
                .Select(modelType => modelType != null)
        );
        
        ValidateApiKeyCommand = ReactiveCommand.CreateFromTask(ValidateApiKeyAsync);
        GenerateLinkCommand = ReactiveCommand.CreateFromTask(GenerateLinkAsync);
        RunMulticallerCommand = ReactiveCommand.CreateFromTask(RunMulticallerAsync);
        StopGradioServerCommand = ReactiveCommand.CreateFromTask(StopGradioServerAsync);
        BackToModelSettingsCommand = ReactiveCommand.CreateFromTask(BackToModelSettingsAsync);
        CopyLastTenMessagesCommand = ReactiveCommand.CreateFromTask(CopyLastTenMessagesAsync);
        CopyAllMessagesCommand = ReactiveCommand.CreateFromTask(CopyAllMessagesAsync);
        ToggleShowTimestampCommand = ReactiveCommand.Create(() => ShowTimestamp = !ShowTimestamp);
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



    private async Task<Unit> AddNewApiKeyAsync()
    {
        try
        {
            // pass delegate _dialogService.PromptUserAsync directly
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

    private Task<Unit> SetPythonPathToLastUsedPythonPathAsync()
    {
        var lastUsedPythonPath = LoadPythonPath();
        PythonPath = lastUsedPythonPath;
        return Task.FromResult(Unit.Default);
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
            async Task DelayedResetBusyFlag()
            {
                try
                {
                    await Task.Delay(10000);
                    await Dispatcher.UIThread.InvokeAsync(() => { IsBusy = false; });
                }
                catch (Exception ex)
                {
                    ConsoleMessageManager.LogError($"Error in DelayedResetBusyFlag: {ex.Message}");
                }
            }

            _ = DelayedResetBusyFlag(); // (btw:  "discard" syntax in C#: explicitly ignoring return values;
                                        // i.e. fire-and-forget without awaiting,  but exceptions are logged!)
        }
    }


    private IAPIHandler? InitializeApiHandler()
    {
        IAPIHandler? apiHandler = null;
        
        // ToDo: Either the title has to act as a unique identifier (which needs to be made clear) or we need to add
        // a unique identifier to the model settings modules. Here the current impl. (via the string .Title):  
        switch (SelectedModelType.Title)
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
                    CurrentNonDataCollectionTab = "Model Settings";
                }
                else
                {
                    ViewManager.SwitchToMulticallerModelSettings();
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
    
    public string LoadPythonPath()
    {
        try
        {
            var pythonPathFile = PathManager.Combine(PathManager.GetBaseDirectory(), "python_path.txt");
            if (!System.IO.File.Exists(pythonPathFile))
            {
                ConsoleMessageManager.LogWarning($"File not found: {pythonPathFile}");
                return string.Empty;
            }

            var path = System.IO.File.ReadAllText(pythonPathFile)?.Trim();
            ConsoleMessageManager.LogInfo($"Python path loaded from {pythonPathFile}");
            return path ?? string.Empty;
        }
        catch (Exception ex)
        {
            AddExceptionMessageToConsole(ex);
            return string.Empty;
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

            ConsoleMessageManager.LogInfo("Multicaller started.");
            IsServerRunning = true;
            ServerStatus = "Multicaller running";
            ServerStatusColor = Brushes.LimeGreen;

            var endMessage = await _apiService.RunMulticallerAsync(ApiKey, CurrentModelSettingsModule);
            ConsoleMessageManager.LogInfo($"Multicaller ended with message: {endMessage}.");
            //ToDo: (Moe) Couldn't this lead to issues? Consider the setter logic for "IsServerRunning"!
            IsServerRunning = false;
            ServerStatus = "Multicaller stopped";
            ServerStatusColor = Brushes.Red;
            DataCollectionManager.LoadChatHistories();

            ViewManager.SwitchToDataCollection();
                
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
            DataCollectionManager.LoadChatHistories();
            ViewManager.SwitchToDataCollection();
            ViewManager.IsLinkGenerationEnabled = false;
        }
        catch (Exception e)
        {
            AddExceptionMessageToConsole(e);
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
            if (SelectedModelType == null)
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
        _timerTime.Stop();
        _timerDate.Stop();
            
        ConsoleMessageManager.PrintGoodbyeMessage();
        _apiService?.Dispose();
        _pythonService?.Dispose();

        var listener = Trace.Listeners.OfType<InternalConsoleTraceListener>().FirstOrDefault();
        if (listener == null) return;
        Trace.Listeners.Remove(listener);
        listener.Dispose();
    }
}