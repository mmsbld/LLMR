using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LLMR.Helpers;

    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double actualHeight && parameter is string percentageStr && double.TryParse(percentageStr, out double percentage))
            {
                return actualHeight * percentage/10;
            }
            return value; // original value if conversion fails 
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }