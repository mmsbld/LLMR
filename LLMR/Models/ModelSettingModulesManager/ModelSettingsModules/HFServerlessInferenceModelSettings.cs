using System.Collections.ObjectModel;
using LLMR.Models.ModelSettingsManager.ModelParameters;
using ReactiveUI;

namespace LLMR.Models.ModelSettingsManager.ModelSettingsModules;

public class HFServerlessInferenceModelSettings : ReactiveObject, IModelSettings
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

    public HFServerlessInferenceModelSettings()
    {
        SelectedModel = "<select a model!>";
        AvailableModels = new ObservableCollection<string>();
        Parameters = new ObservableCollection<ModelParameter>
        {
            new StringParameter
            {
                Name = "System message",
                ValueTyped = "You are a helpful assistant."
            },
            new DoubleParameter
            {
                Name = "Temperature",
                ValueTyped = 0.8,
                Min = 0,
                Max = 2,
                Increment = 0.1
            },
            new DoubleParameter
            {
                Name = "TopP",
                ValueTyped = 1.0,
                Min = 0.0,
                Max = 1.0,
                Increment = 0.05
            },
            new IntParameter
            {
                Name = "MaxCompletionTokens",
                ValueTyped = null, // Optional
                Min = 1
            },
            new DoubleParameter
            {
                Name = "FrequencyPenalty",
                ValueTyped = 0.0,
                Min = -2.0,
                Max = 2.0,
                Increment = 0.1
            },
            new DoubleParameter
            {
                Name = "PresencePenalty",
                ValueTyped = 0.0,
                Min = -2.0,
                Max = 2.0,
                Increment = 0.1
            },
            new StringParameter
            {
                Name = "StopSequences",
                ValueTyped = @"User:" // Default stop sequences
            }
        };
    }
}
