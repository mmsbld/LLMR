using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LLMR.Helpers;

    public class NullToBoolConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value != null;
            if (Invert)
            {
                isVisible = !isVisible;
            }

            return isVisible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
