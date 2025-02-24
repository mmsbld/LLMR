using System;
using System.Globalization;
using Avalonia.Data.Converters;
using LLMR.ViewModels;

namespace LLMR.Helpers;

public class TypedBindingConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is MainWindowViewModel { ShowTimestamp: true } ? 1.0 : 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}