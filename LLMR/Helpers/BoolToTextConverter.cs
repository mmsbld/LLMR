using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LLMR.Helpers;

public class BoolToTextConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 2 && values[0] is bool showTimestamp && values[1] is string text)
        {
            return showTimestamp ? text : string.Empty;
        }
        return string.Empty;
    }

    public object ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}