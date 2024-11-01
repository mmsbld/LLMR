using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LLMR.Helpers;

public class InverseBooleanConverter : IValueConverter
{
    private bool Invert => false;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return Invert ? boolValue : !boolValue;
        }

        return Avalonia.Data.BindingNotification.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return Invert ? boolValue : !boolValue;
        }

        return Avalonia.Data.BindingNotification.UnsetValue;
    }
}