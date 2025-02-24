using ReactiveUI;

namespace LLMR.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private bool _showCompatibleLLMsOnly;
    public bool ShowCompatibleLLMsOnly
    {
        get => _showCompatibleLLMsOnly;
        set => this.RaiseAndSetIfChanged(ref _showCompatibleLLMsOnly, value);
    }

    private string _jsonFilesPath = string.Empty;
    public string JsonFilesPath
    {
        get => _jsonFilesPath;
        set => this.RaiseAndSetIfChanged(ref _jsonFilesPath, value);
    }

    public SettingsViewModel()
    {
        // Initialize defaults or load from configuration
        _showCompatibleLLMsOnly = true;
        _jsonFilesPath = "default/path/to/json"; 
    }
}