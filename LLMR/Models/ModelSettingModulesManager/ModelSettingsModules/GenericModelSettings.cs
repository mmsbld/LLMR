using System.Collections.ObjectModel;
using LLMR.Models.ModelSettingsManager.ModelParameters;

namespace LLMR.Models.ModelSettingsManager.ModelSettingsModules;

public class GenericModelSettings:IModelSettings
{
    public string? SelectedModel { get; set; }
    public string? GeneratedLocalLink { get; set; } = null;
    public string? GeneratedPublicLink { get; set; } = null;
    public ObservableCollection<ModelParameter> Parameters { get; set; } = new ObservableCollection<ModelParameter>();
    public ObservableCollection<string> AvailableModels { get; set; } = new ObservableCollection<string>();
}