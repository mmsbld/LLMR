using System.Collections.ObjectModel;
using LLMR.Model.ModelSettingModulesManager.ModelParameters;
using ReactiveUI;

namespace LLMR.Model.ModelSettingModulesManager.ModelSettingsModules;

public class OpenAI_o1_ModelSettings : ReactiveObject, IModelSettings
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

    public OpenAI_o1_ModelSettings()
    {
        SelectedModel = "<select a model!>";
        AvailableModels = new ObservableCollection<string>();
        Parameters = new ObservableCollection<ModelParameter>
        {
            new BoolParameter
            {
                Name = "Scorn RE parameter",
                ValueTyped = true
            },
            new StringParameter
            {
                Name = "Reasoning Effort",
                ValueTyped = "medium" // allowed values are: low, medium, high (see OpenAI API documentation, Stand Feb 25: "Constrains effort on reasoning for reasoning models. Currently supported values are low, medium, and high. Reducing reasoning effort can result in faster responses and fewer tokens used on reasoning in a response." [https://platform.openai.com/docs/api-reference/chat]
            },
            new IntParameter
            {
                Name = "Max Completion Tokens",
                ValueTyped = null, // Optional parameter (I am not certain, since the reasoning models from OpenAI are not as widely adapted yet, see this discussion: https://community.openai.com/t/o1-models-do-not-support-system-role-in-chat-completion/953880 
                Min = 1
            }
        };
    }
}
