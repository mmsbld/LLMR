using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LLMR.Helpers
{
    public class OpacityToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isVisible)
            {
                return isVisible ? 1.0 : 0.0;
            }

            return 0.0; // (default to invisible if value isn't a boolean)
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}