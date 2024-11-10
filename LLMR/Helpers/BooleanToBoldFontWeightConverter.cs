using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LLMR.Helpers;

public class BooleanToBoldFontWeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool and true ? FontWeight.Bold : FontWeight.Normal;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}