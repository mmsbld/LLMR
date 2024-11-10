using System;
using System.Globalization;
using Avalonia.Data.Converters;
using LLMR.ViewModels;
using System.Collections.Generic;

namespace LLMR.Helpers
{
    public class ShowTimestampTextMultiConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count == 2 && values[0] is MainWindowViewModel vm && values[1] is string timestamp)
            {
                return vm.ShowTimestamp ? timestamp : string.Empty;
            }
            return string.Empty;
        }

        public object ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}