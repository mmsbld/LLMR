using ReactiveUI;

namespace LLMR.Model.ModelSettingModulesManager.ModelParameters;

public abstract class ModelParameter : ReactiveObject
{
    public string Name { get; set; }

    public abstract object? Value { get; set; }
}