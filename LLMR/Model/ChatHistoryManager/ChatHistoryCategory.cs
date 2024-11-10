using System.Collections.ObjectModel;
using ReactiveUI;

namespace LLMR.Model.ChatHistoryManager;

public class ChatHistoryCategory : ReactiveObject
{
    public string Name { get; set; }
    public ObservableCollection<ChatHistoryGroup> Groups { get; set; } = new ObservableCollection<ChatHistoryGroup>();

    public ChatHistoryCategory ParentCategory { get; set; }
    public ObservableCollection<object> Items { get; set; } = new ObservableCollection<object>(); // Can contain ChatHistoryCategory or ChatHistoryFile
    public string FullPath { get; set; }
}