using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace LLMR.Helpers;

public class BooleanToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isExpanded && isExpanded)
        {
            // 30% von windowheight
            return new GridLength(0.3, GridUnitType.Star);
        }
        // Auto height when collapsed^^
        return GridLength.Auto;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
