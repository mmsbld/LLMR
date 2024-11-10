using System.Collections.ObjectModel;
using LLMR.Model.ModelSettingModulesManager.ModelParameters;

namespace LLMR.Model.ModelSettingModulesManager;

public interface IModelSettings
{
    string? SelectedModel { get; set; }
    string? GeneratedLocalLink { get; set; }
    string? GeneratedPublicLink { get; set; }
    ObservableCollection<ModelParameter> Parameters { get; set; }
    ObservableCollection<string> AvailableModels { get; set; } 
}