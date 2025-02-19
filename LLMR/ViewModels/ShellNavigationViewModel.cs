using ReactiveUI;
using System;

namespace LLMR.ViewModels;

/// <summary>
/// Manages which parts (pages/sections) of the UI are currently visible.
/// This replaces the old tab-based approach with a more modern menu or navigation approach.
/// </summary>
public class ShellNavigationViewModel : ViewModelBase
{
    private bool _showLogin;
    private bool _showModelSettings;
    private bool _showMulticallerModelSettings;
    private bool _showLinkGeneration;
    private bool _showDataCollection;

    /// <summary>
    /// Whether the login screen is visible.
    /// </summary>
    public bool ShowLogin
    {
        get => _showLogin;
        set => this.RaiseAndSetIfChanged(ref _showLogin, value);
    }

    /// <summary>
    /// Whether the Model Settings screen is visible (for normal Gradio mode).
    /// </summary>
    public bool ShowModelSettings
    {
        get => _showModelSettings;
        set => this.RaiseAndSetIfChanged(ref _showModelSettings, value);
    }

    /// <summary>
    /// Whether the Model Settings screen is visible (for the Multicaller mode).
    /// </summary>
    public bool ShowMulticallerModelSettings
    {
        get => _showMulticallerModelSettings;
        set => this.RaiseAndSetIfChanged(ref _showMulticallerModelSettings, value);
    }

    /// <summary>
    /// Whether the link generation screen is visible.
    /// </summary>
    public bool ShowLinkGeneration
    {
        get => _showLinkGeneration;
        set => this.RaiseAndSetIfChanged(ref _showLinkGeneration, value);
    }

    /// <summary>
    /// Whether the Data Collection screen is visible.
    /// </summary>
    public bool ShowDataCollection
    {
        get => _showDataCollection;
        set => this.RaiseAndSetIfChanged(ref _showDataCollection, value);
    }

    /// <summary>
    /// Constructor sets the initial screen to Login (as an example).
    /// </summary>
    public ShellNavigationViewModel()
    {
        NavigateToLogin();
    }

    public void NavigateToLogin()
    {
        ShowLogin = true;
        ShowModelSettings = false;
        ShowMulticallerModelSettings = false;
        ShowLinkGeneration = false;
        ShowDataCollection = false;
    }

    public void NavigateToModelSettings()
    {
        ShowLogin = false;
        ShowModelSettings = true;
        ShowMulticallerModelSettings = false;
        ShowLinkGeneration = false;
        ShowDataCollection = false;
    }

    public void NavigateToMulticallerModelSettings()
    {
        ShowLogin = false;
        ShowModelSettings = false;
        ShowMulticallerModelSettings = true;
        ShowLinkGeneration = false;
        ShowDataCollection = false;
    }

    public void NavigateToLinkGeneration()
    {
        ShowLogin = false;
        ShowModelSettings = true;  // we keep model settings available
        ShowMulticallerModelSettings = false;
        ShowLinkGeneration = true;
        ShowDataCollection = false;
    }

    public void NavigateToDataCollection()
    {
        // Data Collection is typically shown in parallel or after certain steps
        // We can keep Model Settings or others on as needed, or do a full switch:
        ShowLogin = false;
        ShowModelSettings = false;
        ShowMulticallerModelSettings = false;
        ShowLinkGeneration = false;
        ShowDataCollection = true;
    }
}