using ReactiveUI;

namespace LLMR.Model;

public class GlobalSettings : ReactiveObject
{
    public static GlobalSettings Instance { get; } = new();

    private bool _showTimestamp = true; 
    public bool ShowTimestamp
    {
        get => _showTimestamp;
        set => this.RaiseAndSetIfChanged(ref _showTimestamp, value);
    }
}
