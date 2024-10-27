using System.Collections.ObjectModel;
using LLMR.Models.ModelSettingsManager.ModelParameters;
using ReactiveUI;

namespace LLMR.Models.ModelSettingsManager.ModelSettingsModules;

public class OpenAIModelSettings : ReactiveObject, IModelSettings
{
    private string? _generatedLocalLink;
    private string? _generatedPublicLink;
    
    private string? _selectedModel;
    public string? SelectedModel
    {
        get => _selectedModel;
        set => this.RaiseAndSetIfChanged(ref _selectedModel, value);
    }
    
    public string? GeneratedLocalLink
    {
        get => _generatedLocalLink;
        set => this.RaiseAndSetIfChanged(ref _generatedLocalLink, value);
    }

    public string? GeneratedPublicLink
    {
        get => _generatedPublicLink;
        set => this.RaiseAndSetIfChanged(ref _generatedPublicLink, value);
    }
    private ObservableCollection<ModelParameter> _parameters;
    public ObservableCollection<ModelParameter> Parameters
    {
        get => _parameters;
        set => this.RaiseAndSetIfChanged(ref _parameters, value);
    }

    private ObservableCollection<string> _availableModels;
    public ObservableCollection<string> AvailableModels
    {
        get => _availableModels;
        set => this.RaiseAndSetIfChanged(ref _availableModels, value);
    }

    public OpenAIModelSettings()
    {
        SelectedModel = "<select a model!>";
        AvailableModels = new ObservableCollection<string>();
        Parameters = new ObservableCollection<ModelParameter>
        {
            new DoubleParameter { Name = "Temperature", Value = 0.7, Min = 0, Max = 1, Increment = 0.1 },
            new IntParameter { Name = "MaxTokens", Value = 100, Min = 1 },
            new DoubleParameter { Name = "TopP", Value = 1.0, Min = 0, Max = 1, Increment = 0.1 },
            new DoubleParameter { Name = "FrequencyPenalty", Value = 0.0, Min = 0, Max = 2, Increment = 0.1 },
            new DoubleParameter { Name = "PresencePenalty", Value = 0.0, Min = 0, Max = 2, Increment = 0.1 }
        };
    }
}