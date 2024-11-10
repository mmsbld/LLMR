using System;
using System.Globalization;
using Avalonia.Data.Converters;
using LLMR.ViewModels;

namespace LLMR.Helpers;

public class ShowTimestampTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MainWindowViewModel vm && parameter is string text)
        {
            return vm.ShowTimestamp ? text : string.Empty;
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}