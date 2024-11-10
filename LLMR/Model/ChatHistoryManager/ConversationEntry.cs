using ReactiveUI;

namespace LLMR.Model.ChatHistoryManager;

public class ConversationEntry : ReactiveObject
{
    private string _label;

    public string Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }
    private string _user;
    public string User
    {
        get => _user;
        set => this.RaiseAndSetIfChanged(ref _user, value);
    }

    private string _assistant;
    public string Assistant
    {
        get => _assistant;
        set => this.RaiseAndSetIfChanged(ref _assistant, value);
    }
}
