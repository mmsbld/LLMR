using System.Collections.ObjectModel;
using LLMR.Models.ModelSettingsManager.ModelParameters;
using ReactiveUI;

namespace LLMR.Models.ModelSettingsManager.ModelSettingsModules;

    public class OpenAI_v2_ModelSettings : ReactiveObject, IModelSettings
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

        public OpenAI_v2_ModelSettings()
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
                    ValueTyped = 0.7,
                    Min = 0,
                    Max = 2,
                    Increment = 0.1
                },
                new DoubleParameter
                {
                    Name = "TopP",
                    ValueTyped = 1.0,
                    Min = 0,
                    Max = 1,
                    Increment = 0.1
                },
                new IntParameter
                {
                    Name = "MaxTokens",
                    ValueTyped = null, 
                    Min = 1
                },
                new DoubleParameter
                {
                    Name = "FrequencyPenalty",
                    ValueTyped = 0.0,
                    Min = -2,
                    Max = 2,
                    Increment = 0.1
                },
                new DoubleParameter
                {
                    Name = "PresencePenalty",
                    ValueTyped = 0.0,
                    Min = -2,
                    Max = 2,
                    Increment = 0.1
                }
            };
        }
    }
