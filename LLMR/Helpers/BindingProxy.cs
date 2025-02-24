using Avalonia;

namespace LLMR.Helpers;

public class BindingProxy<T> : AvaloniaObject
{
    public static readonly StyledProperty<T> DataProperty =
        AvaloniaProperty.Register<BindingProxy<T>, T>(nameof(Data));

    public T Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }
}