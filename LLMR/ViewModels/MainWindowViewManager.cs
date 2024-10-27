using ReactiveUI;

namespace LLMR.ViewModels;

public class MainWindowViewManager : ReactiveObject
{
    #region Fields

    private int _selectedTabIndex;
    private bool _isLoginEnabled;
    private bool _isModelSettingsEnabled;
    private bool _isMulticallerModelSettingsEnabled;
    private bool _isLinkGenerationEnabled;
    private bool _isDataCollectionEnabled;
    private bool _multicallerMode;
    private bool _gradioMode;

    #endregion

    #region Properties

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
    }

    public bool IsLoginEnabled
    {
        get => _isLoginEnabled;
        set => this.RaiseAndSetIfChanged(ref _isLoginEnabled, value);
    }

    public bool IsModelSettingsEnabled
    {
        get => _isModelSettingsEnabled;
        set => this.RaiseAndSetIfChanged(ref _isModelSettingsEnabled, value);
    }

    public bool IsMulticallerModelSettingsEnabled
    {
        get => _isMulticallerModelSettingsEnabled;
        set => this.RaiseAndSetIfChanged(ref _isMulticallerModelSettingsEnabled, value);
    }

    public bool IsLinkGenerationEnabled
    {
        get => _isLinkGenerationEnabled;
        set => this.RaiseAndSetIfChanged(ref _isLinkGenerationEnabled, value);
    }

    public bool IsDataCollectionEnabled
    {
        get => _isDataCollectionEnabled;
        set => this.RaiseAndSetIfChanged(ref _isDataCollectionEnabled, value);
    }
    
    public bool MulticallerMode
    {
        get => _multicallerMode;
        set
        {
            _gradioMode = !value;
            this.RaiseAndSetIfChanged(ref _multicallerMode, value);
        }
    }

    public bool GradioMode
    {
        get => _multicallerMode;
        set
        {
            _multicallerMode = !value;
            this.RaiseAndSetIfChanged(ref _multicallerMode, value);
        }
    }

    #endregion

    #region Constructor

    public MainWindowViewManager()
    {
        // initially true to show tabs (not enabled anyhow):
        MulticallerMode = true;
        GradioMode = true;
        SwitchToLogin();
    }

    #endregion

    #region Methods

    public void SwitchToLogin()
    {
        SelectedTabIndex = 0;
        IsLoginEnabled = true;
        IsModelSettingsEnabled = false;
        IsMulticallerModelSettingsEnabled = false;
        IsLinkGenerationEnabled = false;
        IsDataCollectionEnabled = true;
    }

    public void SwitchToModelSettings()
    {
        SelectedTabIndex = 1;
        IsLoginEnabled = false;
        IsModelSettingsEnabled = true;
        IsMulticallerModelSettingsEnabled = false;
        IsLinkGenerationEnabled = false;
        IsDataCollectionEnabled = true;
    }

    public void SwitchToMulticallerModelSettings()
    {
        SelectedTabIndex = 2;
        IsLoginEnabled = false;
        IsModelSettingsEnabled = false;
        IsMulticallerModelSettingsEnabled = true;
        IsLinkGenerationEnabled = false;
        IsDataCollectionEnabled = true;
    }

    public void SwitchToLinkGeneration()
    {
        SelectedTabIndex = 3;
        IsLoginEnabled = false;
        IsModelSettingsEnabled = true; 
        IsMulticallerModelSettingsEnabled = false;
        IsLinkGenerationEnabled = true;
        IsDataCollectionEnabled = true; 
    }


    public void SwitchToDataCollection()
    {
        SelectedTabIndex = 4;
        IsDataCollectionEnabled = true;
    }


    #endregion
}
