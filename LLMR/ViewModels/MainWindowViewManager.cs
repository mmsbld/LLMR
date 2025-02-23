using System;
using System.Collections.Generic;
using ReactiveUI;

namespace LLMR.ViewModels;

public class MainWindowViewManager : ReactiveObject
{
    private Dictionary<string, string> _tabNames = new()
    {
        {"Login", "Module Selection"},
        {"Model Settings", "Model Settings"},
        {"Multicaller Model Settings", "Multicaller Model Settings"},
        {"Link Generation", "Public model access"},
        {"Data Collection", "File Explorer"}
    }; 
    private string _nameOfCurrentPrimaryTab;
    private string _nameOfCurrentSecondaryTab;
    private bool _isLoginEnabled;
    private bool _isModelSettingsEnabled;
    private bool _isMulticallerModelSettingsEnabled;
    private bool _isLinkGenerationEnabled;
    private bool _isDataCollectionEnabled;
    private bool _multicallerMode;
    private bool _gradioMode;

    public string NameOfCurrentPrimaryTab
    {
        get => _nameOfCurrentPrimaryTab;
        set => this.RaiseAndSetIfChanged(ref _nameOfCurrentPrimaryTab, value);
    }
    public string NameOfCurrentSecondaryTab
    {
        get => _nameOfCurrentSecondaryTab;
        set => this.RaiseAndSetIfChanged(ref _nameOfCurrentSecondaryTab, value);
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
        get => _gradioMode;
        set
        {
            _multicallerMode = !value;
            this.RaiseAndSetIfChanged(ref _multicallerMode, value);
        }
    }
    
    public MainWindowViewManager()
    {
        _nameOfCurrentSecondaryTab = _tabNames["Data Collection"];
        _nameOfCurrentPrimaryTab = _tabNames["Login"];
        // initially true to show tabs (not enabled anyhow)?:
        MulticallerMode = true;
        GradioMode = true;
        SwitchToLogin();
    }
    
    public void SwitchToLogin()
    {
        NameOfCurrentPrimaryTab = _tabNames["Login"];
        IsLoginEnabled = true;
        IsModelSettingsEnabled = false;
        IsMulticallerModelSettingsEnabled = false;
        IsLinkGenerationEnabled = false;
        IsDataCollectionEnabled = false;
    }

    public void SwitchToModelSettings()
    {
        NameOfCurrentPrimaryTab = _tabNames["Model Settings"];
        IsLoginEnabled = false;
        IsModelSettingsEnabled = true;
        IsMulticallerModelSettingsEnabled = false;
        IsLinkGenerationEnabled = false;
        IsDataCollectionEnabled = false;
    }

    public void SwitchToMulticallerModelSettings()
    {
        NameOfCurrentPrimaryTab = _tabNames["Multicaller Model Settings"];
        IsLoginEnabled = false;
        IsModelSettingsEnabled = false;
        IsMulticallerModelSettingsEnabled = true;
        IsLinkGenerationEnabled = false;
        IsDataCollectionEnabled = false;
    }

    public void SwitchToLinkGeneration()
    {
        NameOfCurrentPrimaryTab = _tabNames["Link Generation"];
        IsLoginEnabled = false;
        IsModelSettingsEnabled = false; 
        IsMulticallerModelSettingsEnabled = false;
        IsLinkGenerationEnabled = true;
        IsDataCollectionEnabled = false; 
    }


    public void SwitchToDataCollection()
    {
        IsLoginEnabled = false;
        IsModelSettingsEnabled = false; 
        IsMulticallerModelSettingsEnabled = false;
        IsLinkGenerationEnabled = false;
        IsDataCollectionEnabled = true;
    }

    public void SwitchToSecondaryTab()
    {
        if (_nameOfCurrentSecondaryTab != _tabNames["Data Collection"])
        {
            throw new ArgumentException("<MWVManager.cs> The argument of the secondary tab is not valid.");
        }
        SwitchToDataCollection();
    }

    public void SwitchToPrimaryTab()
    {
        if (NameOfCurrentPrimaryTab == _tabNames["Login"])
        {
            SwitchToLogin();
        }
        else if (NameOfCurrentPrimaryTab == _tabNames["Model Settings"])
        {
            SwitchToModelSettings();
        }
        else if (NameOfCurrentPrimaryTab == _tabNames["Multicaller Model Settings"])
        {
            SwitchToMulticallerModelSettings();
        }
        else if (NameOfCurrentPrimaryTab == _tabNames["Link Generation"])
        {
            SwitchToLinkGeneration();
        }
        else
        {
            throw new ArgumentException("<MWVManager.cs> The argument of the primary tab is not valid.");
        }
    }
    
}
