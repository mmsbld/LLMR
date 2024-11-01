using System.Collections.ObjectModel;
using ReactiveUI;

namespace LLMR.Models.ChatHistoryManager;

public class ChatHistoryGroup : ReactiveObject
{
    public string Date { get; set; }
    public ObservableCollection<ChatHistoryFile> Files { get; set; } = new ObservableCollection<ChatHistoryFile>();
}