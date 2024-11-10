using System;
using ReactiveUI;

namespace LLMR.Model.ModelSettingModulesManager.ModelParameters;

public class DoubleParameter : ModelParameter
{
    private double? _valueTyped;
    public double? ValueTyped
    {
        get => _valueTyped;
        set => this.RaiseAndSetIfChanged(ref _valueTyped, value);
    }

    public override object? Value
    {
        get => _valueTyped;
        set
        {
            if (value is null)
            {
                ValueTyped = null;
            }
            if (value is double dValue)
            {
                ValueTyped = dValue;
            }
            else if (value is IConvertible convertible)
            {
                ValueTyped = convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                throw new InvalidCastException("Value must be of type double.");
            }
        }
    }

    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Increment { get; set; }
}
