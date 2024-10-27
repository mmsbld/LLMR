using System;
using ReactiveUI;

namespace LLMR.Models.ChatHistoryManager;

public class ChatHistoryFile : ReactiveObject
{
    public string Filename { get; set; }
    public DateTime DownloadedOn { get; set; }
    public string FullPath { get; set; }
}
