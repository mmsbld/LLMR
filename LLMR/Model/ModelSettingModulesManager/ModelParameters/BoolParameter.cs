using System;
using ReactiveUI;

namespace LLMR.Model.ModelSettingModulesManager.ModelParameters;

public class BoolParameter : ModelParameter
{
    private bool _valueTyped;
    public bool ValueTyped
    {
        get => _valueTyped;
        set => this.RaiseAndSetIfChanged(ref _valueTyped, value);
    }

    public override object? Value
    {
        get => _valueTyped;
        set
        {
            if (value is bool bValue)
            {
                ValueTyped = bValue;
            }
            else if (value is IConvertible convertible)
            {
                ValueTyped = convertible.ToBoolean(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                throw new InvalidCastException("Value has to be a boolean.");
            }
        }
    }
}
