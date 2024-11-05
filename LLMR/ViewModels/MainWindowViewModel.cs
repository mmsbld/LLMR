using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using QRCoder;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LLMR.Helpers;
using LLMR.Models;
using LLMR.Models.ChatHistoryManager;
using LLMR.Models.ModelSettingsManager;
using LLMR.Models.ModelSettingsManager.ModelSettingsModules;
using LLMR.Services;
using LLMR.Services.HFServerlessInference;
using LLMR.Services.OpenAI_Multicaller;
using LLMR.Services.OpenAI_v2;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using Newtonsoft.Json;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ReactiveUI;
using Color = Avalonia.Media.Color;
using Unit = System.Reactive.Unit;
using LLMR.Views;

namespace LLMR.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    #region Fields

    private string? _serverStatus;
    private IImmutableSolidColorBrush? _serverStatusColor;
    private string? _apiKey;
    private string? _pythonPath;
    private bool _isBusy;
    private int _selectedConsoleIndex;
    private Bitmap? _generatedPublicLinkQRCode;
    private IModelSettings? _modelSettingsModule;
    private string? _selectedModelType;
    private IAPIService? _apiService;
    private bool _isServerRunning;
    private PythonEnvironmentManager? _pythonEnvironmentManager;
    PythonExecutionService? _pythonService;
    private bool _pythonRunning;
    private bool _pythonInitSuccess;

    #endregion

    #region Properties
    
    private bool _isPythonPathLocked;
    public bool IsPythonPathLocked
    {
        get => _isPythonPathLocked;
        set => this.RaiseAndSetIfChanged(ref _isPythonPathLocked, value);
    }


    public string? ApiKey
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
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
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
        set => this.RaiseAndSetIfChanged(ref _modelSettingsModule, value);
    }

    public string? SelectedModelType
    {
        get => _selectedModelType;
        set => this.RaiseAndSetIfChanged(ref _selectedModelType, value);
    }

    public MainWindowViewManager ViewManager { get; private set; }

    public ObservableCollection<string> AvailableModuleTypes { get; set; }

    public ObservableCollection<ConsoleMessage> ConsoleMessages { get; } = new();

    public ChatHistoryCollection ChatHistoryCollection { get; set; }

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
    public ObservableCollection<ApiKeyEntry?> SavedApiKeys { get; } = new ObservableCollection<ApiKeyEntry?>();
    
    private readonly ObservableAsPropertyHelper<bool> _isApiKeySelected;
    public bool IsApiKeySelected => _isApiKeySelected.Value;

    private ApiKeyEntry? _selectedApiKey;
    public ApiKeyEntry? SelectedApiKey
    {
        get => _selectedApiKey;
        set => this.RaiseAndSetIfChanged(ref _selectedApiKey, value);
    }

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> AddNewApiKeyCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveApiKeyCommand { get; }
    public ReactiveCommand<Unit, Unit> EnsurePythonEnvironmentCommand { get; }
    public ReactiveCommand<Unit, Unit>? ConfirmLoginCommand { get; set; }
    public ReactiveCommand<Unit, Unit>? SelectModuleCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ValidateApiKeyCommand { get; }
    public ReactiveCommand<Unit, Unit> GenerateLinkCommand { get; }
    public ReactiveCommand<Unit, Unit> RunMulticallerCommand { get; }
    public ReactiveCommand<Unit, Unit> StopGradioServerCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadAllFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadSelectedAsPdfCommand { get; }
    public ReactiveCommand<Unit, Unit> BackToModelSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyLastMessageCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyAllMessagesCommand { get; }
    
    public ReactiveCommand<Unit, Unit> AddFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveItemCommand { get; }
    public ReactiveCommand<Unit, Unit> RenameItemCommand { get; }
    
    #endregion

    #region Constructor

    public MainWindowViewModel()
    {
        _pythonRunning = false;
        LoadPythonPath();
        
        _pythonEnvironmentManager = new PythonEnvironmentManager();
        _pythonEnvironmentManager.ConsoleMessageOccurred += (message, color) => {
            Dispatcher.UIThread.InvokeAsync(() => AddToConsole(message, color));
        };
        _pythonEnvironmentManager.ExceptionOccurred += (ex) => {
            Dispatcher.UIThread.InvokeAsync(() => AddExceptionMessageToConsole(ex));
        };

        EnsurePythonEnvironmentCommand = ReactiveCommand.CreateFromTask(async () => {
            IsBusy = true;
            await _pythonEnvironmentManager.EnsurePythonEnvironmentAsync();
            PythonPath = _pythonEnvironmentManager.GetPythonLibraryPath();
            IsPythonPathLocked = true; 
            IsBusy = false;
        });
        
        AvailableModuleTypes = new ObservableCollection<string> { "OpenAI", "Hugging Face Serverless Inference", "OpenAI Multicaller" };
        SelectedModelType = "OpenAI"; // Default selection

        ViewManager = new MainWindowViewManager();
        
        ServerStatus = "Stopped";
        ServerStatusColor = Brushes.Red;

        ChatHistoryCollection = new ChatHistoryCollection();
        
        _isApiKeySelected = this.WhenAnyValue(x => x.SelectedApiKey)
            .Select(apiKey => apiKey is not null)
            .ToProperty(this, x => x.IsApiKeySelected);

        AddNewApiKeyCommand = ReactiveCommand.CreateFromTask(AddNewApiKeyAsync);
        RemoveApiKeyCommand = ReactiveCommand.Create(RemoveSelectedApiKey, this.WhenAnyValue(x => x.IsApiKeySelected));
        
        CreateOrResetConfirmLoginCommand();
        CreateOrResetSelectModuleCommand();
        
        ValidateApiKeyCommand = ReactiveCommand.CreateFromTask(ValidateApiKeyAsync);
        GenerateLinkCommand = ReactiveCommand.Create(GenerateLink);
        RunMulticallerCommand = ReactiveCommand.Create(RunMulticaller);
        StopGradioServerCommand = ReactiveCommand.Create(StopServersAndSwitchToDataCollection);
        CopyLastMessageCommand = ReactiveCommand.CreateFromTask(CopyLastMessageAsync);
        CopyAllMessagesCommand = ReactiveCommand.CreateFromTask(CopyAllMessagesAsync);
        BackToModelSettingsCommand = ReactiveCommand.Create(BackToModelSettings);
        AddFolderCommand = ReactiveCommand.CreateFromTask(AddFolderAsync);
        RemoveItemCommand = ReactiveCommand.Create(RemoveItem);
        RenameItemCommand = ReactiveCommand.CreateFromTask(RenameItemAsync);


        IsBusy = false;

        LoadApiKeys();
        LoadChatHistories(); // what will happen with files that are written during the session?
        SetupFileWatcher();

        DownloadAllFilesCommand = ReactiveCommand.CreateFromTask(DownloadAllFilesAsync);
        DownloadSelectedAsPdfCommand = ReactiveCommand.CreateFromTask(DownloadSelectedAsPdfAsync);

        QuestPDF.Settings.License = LicenseType.Community; // we can obviously use the MIT community license, right? 

        ViewManager.SwitchToLogin();
        
        AddSuccessMessageToConsole("<init complete>");
        DisplayStartupMessages();
        
        Trace.Listeners.Add(new InternalConsoleTraceListener(message => AddToConsole(message, new SolidColorBrush(Colors.Gray))));
    }

    #endregion

    #region Methods
    
    private void DisplayStartupMessages()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        var osDescription = RuntimeInformation.OSDescription;
        var osName = "Unknown OS";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            osName = "macOS";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            osName = "Linux";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            osName = "Windows";
        }


        var currentDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var timezone = TimeZoneInfo.Local.DisplayName;

        const string listOfHyphens = "– – – – – –";
        const string lineOfStars = "*************";
        var appInfo = $"LLMR v{version} running on {osName} ({osDescription}).";
        var dateTime = $"Current time: {currentDate} ({timezone}).";

        const string networkWarningLineOne = "Please make sure that your connection is stable and all required ports are open.";
        const string networkWarningLineTwo = "In public networks (e.g., schools or universities), some ports may be restricted.";
        const string networkWarningLineThree = "Consider using a private network, such as a mobile hotspot for running LLMR.";
        const string networkWarningLineFour = "The client interface is not affected, clients can use school or university networks.";
        const string networkWarningLineFive = "In spite of LLMR running in a private network, for the chat histories are saved locally. ";

        AddToConsole(lineOfStars, new SolidColorBrush(Colors.DarkGreen));
        AddToConsole(appInfo, new SolidColorBrush(Colors.DarkGreen)); 
        AddToConsole(dateTime, new SolidColorBrush(Colors.DarkGreen)); 
        AddToConsole(lineOfStars, new SolidColorBrush(Colors.DarkGreen));

        AddToConsole(listOfHyphens, new SolidColorBrush(Colors.Tomato));
        AddToConsole(networkWarningLineOne, new SolidColorBrush(Colors.Tomato));
        AddToConsole(networkWarningLineTwo, new SolidColorBrush(Colors.Tomato));
        AddToConsole(networkWarningLineThree, new SolidColorBrush(Colors.Tomato));
        AddToConsole(networkWarningLineFour, new SolidColorBrush(Colors.Tomato));
        AddToConsole(networkWarningLineFive, new SolidColorBrush(Colors.Tomato));
        AddToConsole(listOfHyphens, new SolidColorBrush(Colors.Tomato));
    }
    
    private void LoadPythonPath()
    {
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pythonpath.txt");
        PythonPath = File.Exists(filePath) ? File.ReadAllText(filePath) : "/Library/Frameworks/Python.framework/Versions/3.12"; // default value
    }

    private void SavePythonPath()
    {
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pythonpath.txt");
        File.WriteAllText(filePath, PythonPath);
    }

    
    private async Task AddNewApiKeyAsync()
    {
        var name = await PromptUserAsync("Enter a name for the API key:");
        if (string.IsNullOrEmpty(name))
        {
            AddToConsole("API key addition canceled by the user.");
            return;
        }

        var key = await PromptUserAsync("Enter the API key:");
        if (string.IsNullOrEmpty(key))
        {
            AddToConsole("API key addition canceled by the user.");
            return;
        }

        var newEntry = new ApiKeyEntry { Name = name, Key = key };
        SavedApiKeys.Add(newEntry);
        SelectedApiKey = newEntry;
        SaveApiKeys();
    }
    
    private void RemoveSelectedApiKey()
    {
        if (SelectedApiKey != null)
        {
            SavedApiKeys.Remove(SelectedApiKey);
            SelectedApiKey = null;
            SaveApiKeys();
        }
    }

    private async Task ConfirmLoginAsync()
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
                AddToConsole("Python initialization failed. Please check the Python path and try again.");
            }

            if (SavedApiKeys.Count == 0)
                throw new ArgumentException("Saved API keys are empty.");
    
            foreach (var apiKey in SavedApiKeys)
            {
                Debug.Assert(apiKey != null, nameof(apiKey) + " != null");
                apiKey.IsLastUsed = apiKey == SelectedApiKey;
            }
        }
        catch (Exception e)
        {
            AddExceptionMessageToConsole(e);
        }
        finally
        {
            Dispatcher.UIThread.InvokeAsync(() => IsBusy = false);
        }
    }




    private async Task<string> PromptUserAsync(string message)
    {
        var inputDialog = new InputDialog
        {
            ViewModel =
            {
                Title = "Input Required",
                Message = message
            }
        };

        var result = await inputDialog.ShowDialog<string>(GetMainWindow());

        return result;
    }


    private void LoadApiKeys()
    {
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apikeys.json");
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            var keys = JsonConvert.DeserializeObject<List<ApiKeyEntry>>(json);
            if (keys == null) return;
            foreach (var key in keys)
            {
                SavedApiKeys.Add(key);
            }
            SelectedApiKey = SavedApiKeys.FirstOrDefault(k => k != null && k.IsLastUsed) ?? SavedApiKeys.FirstOrDefault();
        }
    }

    private void SaveApiKeys()
    {
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apikeys.json");
        var json = JsonConvert.SerializeObject(SavedApiKeys, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    private async Task<bool> InstantiateModuleWithInstanceOfIAPIHandlerAsync()
    {
        if (_pythonService != null)
        {
            AddToConsole("PES is already initialized.");
            return _pythonRunning;
        }

        try
        {
            AddToConsole("Initializing PES...");
            _pythonService = PythonExecutionService.GetInstance(PythonPath);
            AddToConsole("PES instantiated successfully.");

            _pythonService.ExceptionOccurred += (_, errorMessage) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AddExceptionMessageToConsole(new Exception($"<PES> Error: {errorMessage}"));
                    //_pythonRunning = false;
                    //_pythonService = null; // reset to allow new singleton (PES.cs)
                    //IsPythonPathLocked = false;
                });
            };

            _pythonService.ConsoleMessageOccurred += (_, message) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SolidColorBrush color;
                    if (message.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("exception", StringComparison.OrdinalIgnoreCase))
                    {
                        color = new SolidColorBrush(Colors.DarkRed);
                    }
                    else if (message.Contains("successfully", StringComparison.OrdinalIgnoreCase))
                    {
                        color = new SolidColorBrush(Colors.ForestGreen);
                    }
                    else
                    {
                        color = new SolidColorBrush(Colors.DarkSalmon);
                    }

                    AddToConsole(message, color);
                });
            };

            var initSuccess = await _pythonService.InitializationTask;

            if (!initSuccess)
            {
                AddToConsole("PES failed to initialize.");
                _pythonService = null; // reset for new singleton (pes.cs)
                IsPythonPathLocked = false;
                return false;
            }

            IAPIHandler? apiHandler = null;
            switch (SelectedModelType)
            {
                case "OpenAI":
                    CurrentModelSettingsModule = new OpenAI_v2_ModelSettings();
                    apiHandler = new OpenAI_v2_APIHandler(_pythonService, PythonPath);
                    _apiService = new APIService(apiHandler);
                    ViewManager.GradioMode = true;
                    break;
                case "Hugging Face Serverless Inference":
                    CurrentModelSettingsModule = new HFServerlessInferenceModelSettings();
                    apiHandler = new HFServerlessInference_APIHandler(_pythonService, PythonPath);
                    _apiService = new APIService(apiHandler);
                    ViewManager.GradioMode = true;
                    break;
                case "OpenAI Multicaller":
                    CurrentModelSettingsModule = new OpenAI_Multicaller_ModelSettings();
                    apiHandler = new OpenAI_Multicaller_APIHandler(_pythonService, PythonPath);
                    _apiService = new OpenAI_Multicaller_APIService(apiHandler);
                    ViewManager.MulticallerMode = true;
                    break;
            }

            if (_apiService == null)
            {
                AddExceptionMessageToConsole(new ArgumentNullException("PES is uninitializable, since APIS is not initialized."));
                _pythonRunning = false;
                return false;
            }

            _apiService.ConsoleMessageOccured += (_, args) =>
                Dispatcher.UIThread.InvokeAsync(() => AddToConsole("<APIS> " + args));

            _apiService.ErrorMessageOccured += (_, args) =>
                Dispatcher.UIThread.InvokeAsync(() =>
                    AddToConsole("<APIS error> " + args, new SolidColorBrush(Colors.DarkRed)));

            AddToConsole("PES initialized and running.", new SolidColorBrush(Colors.DarkSalmon));
            _pythonRunning = true;
            IsPythonPathLocked = true; 
            return true; 
        }
        catch (Exception e)
        {
            AddExceptionMessageToConsole(e);
            _pythonRunning = false;
            _pythonService = null; // reset for new singleton (pes.cs)!
            IsPythonPathLocked = false; 
            return false; 
        }
        finally
        {
            Dispatcher.UIThread.InvokeAsync(() => { IsBusy = false; });
        }
    }






    private async Task ValidateApiKeyAsync()
    {
        if (!_pythonRunning)
            return;
        try
        {
            if (_apiService == null)
            {
                throw new Exception(new StringBuilder().Append("<< FATAL CRASH >> << TRY TO CLOSE AND REOPEN THE APPLICATION! >>: _apiService is uninitialized but _pythonRunning is flagged as true.").ToString());
            }
            Dispatcher.UIThread.InvokeAsync(() => { AddToConsole("<MWVM> Validating API Key ..."); });
            IsBusy = true;

            if (string.IsNullOrEmpty(ApiKey))
            {
                throw new ArgumentException("Could not find an API key / token. Did you enter one?");
            }

            bool validApiKey;
            try
            {
                validApiKey = await _apiService.ValidateApiKeyAsync(ApiKey);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException($"Error validating API key: {ex.Message}", ex);
            }


            if (validApiKey)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AddSuccessMessageToConsole("Validated API key / token successfully.");
                });
                
                if (CurrentModelSettingsModule == null)
                    throw new NullReferenceException("CurrentModelSettingsModule is null.");

                if (CurrentModelSettingsModule.GetType() != typeof(OpenAI_Multicaller_ModelSettings))
                    ViewManager.SwitchToModelSettings();
                else
                    ViewManager.SwitchToMulticallerModelSettings();

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
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddExceptionMessageToConsole(
                    new Exception($"Validation failed due to an error in the API service: {ex.Message}", ex));
            });
        }
        catch (ArgumentException ex)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddExceptionMessageToConsole(ex);
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddExceptionMessageToConsole(new Exception($"An unexpected error occurred: {ex.Message}", ex));
            });
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    void CreateOrResetConfirmLoginCommand()
    {
        var canExecute = this.WhenAnyValue(vm => vm.SelectedApiKey)
            .Select(apiKey => apiKey != null);
    
        ConfirmLoginCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                try
                {
                    await ConfirmLoginAsync();
                }
                catch (Exception ex)
                {
                    AddExceptionMessageToConsole(ex);
                }
            },
            canExecute
        );
    }



    void CreateOrResetSelectModuleCommand()
    {
        var canExecute = this.WhenAnyValue(vm => vm.SelectedModelType)
            .Select(modelType => !string.IsNullOrEmpty(modelType));
        
        SelectModuleCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                try
                {
                    await InstantiateModuleWithInstanceOfIAPIHandlerAsync();
                }
                catch (Exception ex)
                {
                    AddExceptionMessageToConsole(ex);
                }
                finally
                {
                    Dispatcher.UIThread.InvokeAsync(() => { CreateOrResetSelectModuleCommand();});
                }
            },
            canExecute
        );
        
        SelectModuleCommand.IsExecuting.Subscribe(isExecuting =>
        {
            IsBusy = isExecuting;
            Dispatcher.UIThread.InvokeAsync(() => { AddToConsole($"<MWVM> SelectModuleCommand State of IsExecuting is: {isExecuting}", new SolidColorBrush(Colors.DarkSalmon)); });
        });
        
        SelectModuleCommand.CanExecute.Subscribe(canExecute =>
        {
            Dispatcher.UIThread.InvokeAsync(() => { AddToConsole($"<MWVM> SelectModuleCommand State of CanExecute is: {canExecute}", new SolidColorBrush(Colors.OliveDrab)); });
        });
        
        SelectModuleCommand.ThrownExceptions.Subscribe(ex =>
        {
            Dispatcher.UIThread.InvokeAsync(() => { AddExceptionMessageToConsole(ex);});
            Dispatcher.UIThread.InvokeAsync(CreateOrResetSelectModuleCommand);
        });
    }

    private async void GenerateLink()
    {
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
            AddToConsole($"<internal> Local Link: {localLink}, Public Link: {publicLink}");

            if (CurrentModelSettingsModule == null)
                throw new NullReferenceException("CurrentModelSettingsModule is null.");
            
            CurrentModelSettingsModule.GeneratedLocalLink = localLink;
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

            IsBusy = false;
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
    }

    private async void RunMulticaller()
    {
        try
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                throw new ArgumentException("Please enter a valid API key / token.");
            }

            if (_apiService == null)
                throw new NullReferenceException("API Service is null.");
            
            if (_apiService.GetType() != typeof(OpenAI_Multicaller_APIService))
                throw new NotSupportedException("<MWVM> API Service type is not supported.");

            var apiService = (OpenAI_Multicaller_APIService)_apiService;

            AddToConsole($"<internal> Multicaller started.");
            IsServerRunning = true;
            ServerStatus = "Multicaller running";
            ServerStatusColor = Brushes.LimeGreen;

            ViewManager.SwitchToDataCollection();
            ViewManager.IsLoginEnabled = false;
            ViewManager.IsModelSettingsEnabled = false;
            ViewManager.IsMulticallerModelSettingsEnabled = false;
            ViewManager.IsLinkGenerationEnabled = false;
            ViewManager.IsDataCollectionEnabled = true;
            
            var endMessage = await apiService.RunMulticallerAsync(ApiKey, CurrentModelSettingsModule);
            LoadChatHistories();
            AddSuccessMessageToConsole($"<internal> Multicaller ended with message: {endMessage}.");
            IsServerRunning = false; 
            ServerStatus = "Multicaller stopped";
            ServerStatusColor = Brushes.Red;
            
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
    }

    private async void BackToModelSettings()
    {
        await StopGradioServer();
        IsServerRunning = false;
        ViewManager.SwitchToModelSettings();
    }

    private async void StopServersAndSwitchToDataCollection()
    {
        try
        {
            if (_apiService == null) return;
            await StopGradioServer();
            IsServerRunning = false;
            SwitchToDataCollectionAndLoadChatHistories();
        }
        catch (Exception e)
        {
            AddExceptionMessageToConsole(e);
        }
    }

    private void SwitchToDataCollectionAndLoadChatHistories()
    {
        LoadChatHistories();
        ViewManager.SwitchToDataCollection();
        
        if (SelectedModelType == "OpenAI Multicaller")
        {
            ViewManager.IsMulticallerModelSettingsEnabled = true;
        }
    }

    private async Task StopGradioServer()
    {
        AddToConsole("<MWVM> Stopping server...");
        if (_apiService == null)
            throw new NullReferenceException("API Service is null.");
        var stopMessage = await _apiService.StopGradioInterfaceAsync();
        AddSuccessMessageToConsole("Server stopped with message: " + stopMessage);
        ViewManager.SwitchToDataCollection();
        ViewManager.IsLinkGenerationEnabled = false;
    }

    #region ConsoleMessaging
    
    private async Task CopyLastMessageAsync()
    {
        if (ConsoleMessages.Any())
        {
            var lastMessage = ConsoleMessages.Last();
            var textToCopy = $"[{lastMessage.Timestamp}] {lastMessage.Text}";
            var clipboard = Clipboard.Get();
            await clipboard.SetTextAsync(textToCopy);
        }
    }

    private async Task CopyAllMessagesAsync()
    {
        var allMessages = string.Join(Environment.NewLine, ConsoleMessages.Select(msg => $"[{msg.Timestamp}] {msg.Text}"));
        var clipboard = Clipboard.Get();
        await clipboard.SetTextAsync(allMessages);
    }

    private void AddToConsole(string message)
    {
        AddToConsole("<MWVM message> " + message, new SolidColorBrush(Color.Parse("#3F51B5")));
    }

    private void AddToConsole(string message, SolidColorBrush color)
    {
        var messageColor = color;
        var processedMessage = message;

        var stdoutIndex = message.IndexOf("<PES stdout>", StringComparison.Ordinal);
        var stderrIndex = message.IndexOf("<PES stderr>", StringComparison.Ordinal);

        if (stdoutIndex >= 0)
        {
            processedMessage = message.Substring(stdoutIndex + "<PES stdout>".Length).Trim();
            messageColor = new SolidColorBrush(Colors.LightGray);
        }
        else if (stderrIndex >= 0)
        {
            processedMessage = message.Substring(stderrIndex + "<PES stderr>".Length).Trim();
            messageColor = new SolidColorBrush(Colors.DarkGray);
        }

        ConsoleMessages.Add(new ConsoleMessage
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            Text = processedMessage,
            Color = messageColor
        });
        SelectedConsoleIndex = ConsoleMessages.Count - 1;
    }



    private void AddExceptionMessageToConsole(Exception exception)
    {
        if (exception.Message.Contains("<PES stderr>"))
        {
            AddToConsole(exception.Message, new SolidColorBrush(Colors.DarkGray));
        }
        else
        {
            AddToConsole("<MWVM error> " + exception.Message, new SolidColorBrush(Colors.DarkRed));
        }
    }


    private void AddSuccessMessageToConsole(string message)
    {
        if (message.Contains("<PES stdout>"))
        {
            AddToConsole(message, new SolidColorBrush(Colors.LightGray));
        }
        else
        {
            AddToConsole("<MWVM> " + message, new SolidColorBrush(Colors.ForestGreen));
        }
    }
    #endregion


    #region ConversationHistoryViewing

    private void LoadChatHistories()
    {
        var directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "chat_histories");
        ChatHistoryCollection.LoadFiles(directoryPath);
    }

    private void SetupFileWatcher()
    {
        var directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "chat_histories");
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var watcher = new FileSystemWatcher(directoryPath, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
        };

        watcher.Changed += OnFilesChanged;
        watcher.Created += OnFilesChanged;
        watcher.Deleted += OnFilesChanged;
        watcher.Renamed += OnFilesChanged;
        watcher.EnableRaisingEvents = true;
    }
    
    private async Task AddFolderAsync()
    {
        var folderName = await PromptUserAsync("Enter the name of the new folder:");
        if (string.IsNullOrWhiteSpace(folderName))
        {
            AddToConsole("Folder creation canceled by the user.");
            return;
        }

        try
        {
            ChatHistoryCollection.AddFolder(folderName);
            AddToConsole($"Folder '{folderName}' added successfully.");
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
            AddToConsole("Item removed successfully.");
        }
        catch (Exception ex)
        {
            AddExceptionMessageToConsole(ex);
        }
    }

    private async Task RenameItemAsync()
    {
        var newName = await PromptUserAsync("Enter the new name:");
        if (string.IsNullOrWhiteSpace(newName))
        {
            AddToConsole("Rename operation canceled by the user.");
            return;
        }

        try
        {
            ChatHistoryCollection.RenameItem(ChatHistoryCollection.SelectedFile, newName);
            AddToConsole("Item renamed successfully.");
        }
        catch (Exception ex)
        {
            AddExceptionMessageToConsole(ex);
        }
    }


    private void OnFilesChanged(object sender, FileSystemEventArgs e)
    {
        // add delay to ensure file has been saved (.5 s)
        Task.Delay(500).ContinueWith(_ => ChatHistoryCollection.LoadFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "chat_histories")));
    }

    private async Task DownloadAllFilesAsync()
    {
        try
        {
            var topLevel = App.TopLevel;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose a folder to copy all chat histories to."
            });

            if (folders.Count > 0)
            {
                var targetDirectory = folders[0].Path.LocalPath;
                var sourceDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "chat_histories");

                foreach (var file in Directory.GetFiles(sourceDirectory, "*.json"))
                {
                    var destFile = Path.Combine(targetDirectory, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }

                await ShowMessageAsync("Download successful", "All JSON-files were successfully downloaded.");
            }
            else
            {
                AddToConsole("No directory selected.");
            }
        }
        catch (Exception ex)
        {
            AddToConsole($"Error: {ex.Message}");
            await ShowMessageAsync("Download was not successful", $"There was an error: {ex.Message}");
        }
    }

    private async Task DownloadSelectedAsPdfAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(ChatHistoryCollection.SelectedFile.Filename))
            {
                await ShowMessageAsync("No file chosen", "Please select a chat history to download.");
                return;
            }

            var topLevel = App.TopLevel;

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

                AddToConsole($"PDF is saved under {pdfPath}.");

                GeneratePdf(pdfPath);

                await ShowMessageAsync("Export successful", "The chosen chat history was successfully exported as PDF.");
            }
            else
            {
                AddToConsole("No chat history selected.");
            }
        }
        catch (Exception ex)
        {
            AddToConsole($"Error: {ex.Message}");
            await ShowMessageAsync("Download not successful", $"There was an error: {ex.Message}");
        }
    }

    private void GeneratePdf(string pdfPath)
    {
        if (ChatHistoryCollection == null)
        {
            throw new InvalidOperationException("ChatHistoryCollection is null.");
        }

        if (ChatHistoryCollection.Settings == null)
        {
            throw new InvalidOperationException("Settings are null.");
        }

        if (ChatHistoryCollection.Conversation == null || ChatHistoryCollection.Conversation.Count == 0)
        {
            throw new InvalidOperationException("No conversation entries in selected chat history.");
        }

        try
        {
            var pdf = new ChatHistoryDocument(ChatHistoryCollection);
            pdf.GeneratePdf(pdfPath);
        }
        catch (Exception ex)
        {
            AddExceptionMessageToConsole(ex);
        }
    }

    private Window GetMainWindow()
    {
        var appCurrent = Application.Current;
        if (appCurrent is null)
            throw new NullReferenceException("Application.Current is null.");
        var mainWindow = (appCurrent.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (mainWindow != null)
            return mainWindow;
        throw new NullReferenceException("mainWindow is null.");
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var messageBoxCustomWindow = MessageBoxManager.GetMessageBoxCustom(
            new MessageBoxCustomParams
            {
                ContentTitle = title,
                ContentMessage = message,
                ButtonDefinitions = new[]
                {
                    new ButtonDefinition { Name = "OK" }
                },
                Icon = MsBox.Avalonia.Enums.Icon.Info,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                SizeToContent = SizeToContent.WidthAndHeight
            });

        await messageBoxCustomWindow.ShowWindowDialogAsync(GetMainWindow());
    }

    #endregion

    #endregion
    
    public void Dispose()
    {
        _apiService?.Dispose();
        _pythonService?.Dispose();
        
        var listener = Trace.Listeners.OfType<InternalConsoleTraceListener>().FirstOrDefault();
        if (listener == null) return;
        Trace.Listeners.Remove(listener);
        listener.Dispose();
    }

}