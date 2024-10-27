using System;
using ReactiveUI;

namespace LLMR.Models.ModelSettingsManager.ModelParameters;

public class StringParameter : ModelParameter
{
    private string _valueTyped;
    public string ValueTyped
    {
        get => _valueTyped;
        set => this.RaiseAndSetIfChanged(ref _valueTyped, value);
    }

    public override object? Value
    {
        get => _valueTyped;
        set
        {
            if (value is string sValue)
            {
                ValueTyped = sValue;
            }
            else if (value is null)
            {
                throw new InvalidCastException("Value cannot be null.");
            }
            else
            {
                ValueTyped = value?.ToString() ?? throw new InvalidOperationException();
            }
        }
    }
}
