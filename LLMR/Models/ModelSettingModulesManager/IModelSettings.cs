using System.Collections.ObjectModel;
using LLMR.Models.ModelSettingsManager.ModelParameters;

namespace LLMR.Models.ModelSettingsManager;

public interface IModelSettings
{
    string? SelectedModel { get; set; }
    string? GeneratedLocalLink { get; set; }
    string? GeneratedPublicLink { get; set; }
    ObservableCollection<ModelParameter> Parameters { get; set; }
    ObservableCollection<string> AvailableModels { get; set; } 
}