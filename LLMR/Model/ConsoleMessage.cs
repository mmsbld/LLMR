using Avalonia.Media;
using ReactiveUI;

namespace LLMR.Model;

public class ConsoleMessage : ReactiveObject
{
    public string Timestamp { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public SolidColorBrush Color { get; set; } = new SolidColorBrush(Colors.Black);

    private bool _isBold;
    public bool IsBold
    {
        get => _isBold;
        set => this.RaiseAndSetIfChanged(ref _isBold, value);
    }

    public bool ShowTimestamp => GlobalSettings.Instance.ShowTimestamp;

    public ConsoleMessage()
    {
        GlobalSettings.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(GlobalSettings.ShowTimestamp))
            {
                this.RaisePropertyChanged(nameof(ShowTimestamp));
            }
        };
    }
}
