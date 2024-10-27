using Avalonia.Media;
using ReactiveUI;

namespace LLMR.Models;

public class ConsoleMessage : ReactiveObject
{
    public string Timestamp { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public SolidColorBrush Color { get; set; } = new SolidColorBrush(Colors.Black);
}
