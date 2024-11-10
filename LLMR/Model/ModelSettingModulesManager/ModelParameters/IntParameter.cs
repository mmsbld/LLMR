using System;
using ReactiveUI;

namespace LLMR.Model.ModelSettingModulesManager.ModelParameters;

public class IntParameter : ModelParameter
{
    private int? _valueTyped;
    public int? ValueTyped
    {
        get => _valueTyped;
        set => this.RaiseAndSetIfChanged(ref _valueTyped, value);
    }

    public override object? Value
    {
        get => _valueTyped;
        set
        {
            if (value == null)
            {
                ValueTyped = null;
            }
            if (value is int iValue)
            {
                ValueTyped = iValue;
            }
            else if (value is IConvertible convertible)
            {
                ValueTyped = convertible.ToInt32(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                throw new InvalidCastException("Value must be of type integer.");
            }
        }
    }

    public int? Min { get; set; }
    public int? Max { get; set; }
}
