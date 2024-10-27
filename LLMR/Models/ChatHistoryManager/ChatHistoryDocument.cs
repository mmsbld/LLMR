using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LLMR.Models.ChatHistoryManager;

public class ChatHistoryDocument : IDocument
{
    public ChatHistoryCollection Model { get; }

    public ChatHistoryDocument(ChatHistoryCollection model)
    {
        Model = model;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(50);
            
            page.Header().Height(50).Background(Colors.Grey.Lighten1).Text("Chat History");

            page.Content().Column(column =>
            {
                foreach (var entry in Model.Conversation)
                {
                    column.Item().Text($"User: {entry.User}");
                    column.Item().Text($"Assistant: {entry.Assistant}");
                }
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        });
    }
}
