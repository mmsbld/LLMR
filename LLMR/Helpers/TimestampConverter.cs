using System;
using System.Globalization;
using Avalonia.Data.Converters;
using System.Collections.Generic;

namespace LLMR.Helpers;

public class TimestampConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 2 && values[0] is bool showTimestamp && values[1] is string timestamp)
        {
            return showTimestamp ? timestamp : string.Empty;
        }
        return string.Empty;
    }
}