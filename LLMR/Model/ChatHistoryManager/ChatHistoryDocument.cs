using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace LLMR.Model.ChatHistoryManager
{
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

                // Header: logo and title
                page.Header().Row(row =>
                {
                    row.ConstantItem(100)
                        .Image("Assets/logo/logo_full.png", ImageScaling.FitArea);
                    row.RelativeItem()
                        .AlignCenter()
                        .Text("Chat History")
                        .FontSize(24)
                        .SemiBold();
                });

                // Content: list of conversation entries
                page.Content().Column(column =>
                {
                    foreach (var entry in Model.Conversation)
                    {
                        if (!string.IsNullOrEmpty(entry.User))
                            column.Item().Text($"User: {entry.User}");
                        if (!string.IsNullOrEmpty(entry.Assistant))
                            column.Item().Text($"Assistant: {entry.Assistant}");
                        column.Item().PaddingVertical(5);
                    }
                });

                // Footer: page numbers.
                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }
    }
}