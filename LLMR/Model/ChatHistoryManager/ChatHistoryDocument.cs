using System;
using System.Globalization;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace LLMR.Model.ChatHistoryManager;

public class ChatHistoryDocument : IDocument
{
    // load the logo image as a byte array (see github example QUestPDF)
    public static byte[] LogoBytes { get; } = File.ReadAllBytes("Assets/logo/logo_full.png");

    public ChatHistoryCollection Model { get; }
    public ChatHistoryDocument(ChatHistoryCollection model) => Model = model;
        
    public DocumentMetadata GetMetadata() => new DocumentMetadata
    {
        Title = "Chat History - LLMRunner (v0.6)",
        Author = "LLMRunner (pre-alpha) - Moritz Seibold",
        Subject = "Chat History Export",
        Keywords = "LLMR, Chat History, PDF",
        Creator = "LLMRunner",
        Producer = "LLMRunner",
        CreationDate = DateTimeOffset.Now,
        ModifiedDate = DateTimeOffset.Now
    };

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(50); // page size??? a4?? nothing?
                
            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer()
                .AlignCenter()
                .Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(12).FontColor("#181851"));
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
        });
    }

    void ComposeHeader(IContainer container)
    {
        var darkBlue = "#181851";
        // Use a Column to group the header row and the horizontal line into one container.
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(80)
                    .Height(80)
                    .Image(LogoBytes, ImageScaling.FitArea);
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(text =>
                    {
                        text.Span("LLMRunner")
                            .FontSize(24)
                            .Bold()
                            .FontColor(darkBlue);
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Version 0.6")
                            .FontSize(12)
                            .FontColor(darkBlue);
                    });
                    column.Item().Text(text =>
                    {
                        text.Span(DateTime.Now.ToString("f", CultureInfo.CurrentCulture))
                            .FontSize(12)
                            .FontColor(darkBlue);
                    });
                });
            });
            // horizontal line  
            col.Item().PaddingBottom(5).LineHorizontal(1).LineColor(darkBlue);
        });
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Spacing(20);
            // Chat Details Section
            column.Item().Element(ComposeDetails);
            // Conversation Section
            column.Item().Element(ComposeConversation);
        });
    }

    void ComposeDetails(IContainer container)
    {
        var darkBlue = "#181851";
        container.Column(column =>
        {
            column.Spacing(5);
            column.Item().Text(text =>
            {
                text.Span("Model Settings")
                    .FontSize(18)
                    .Bold()
                    .FontColor(darkBlue);
            });
            if (Model.Settings != null)
            {
                column.Item().Text(text =>
                {
                    text.Span($"Model: {Model.Settings.SelectedModel}")
                        .FontSize(14)
                        .FontColor(darkBlue);
                });
                if (Model.Settings.Parameters != null && Model.Settings.Parameters.Any())
                {
                    column.Item().Text(text =>
                    {
                        text.Span("Parameters:")
                            .FontSize(14)
                            .Bold()
                            .FontColor(darkBlue);
                    });
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(150);
                            columns.RelativeColumn();
                        });
                        foreach (var param in Model.Settings.Parameters)
                        {
                            table.Cell().Element(cell => CellStyle(cell, darkBlue))
                                .Text(text => text.Span(param.Name));
                            table.Cell().Element(cell => CellStyle(cell, darkBlue))
                                .Text(text => text.Span(param.Value?.ToString() ?? string.Empty));
                        }
                    });
                }
            }
            if (!string.IsNullOrEmpty(Model.ApiKey))
            {
                column.Item().Text(text =>
                {
                    text.Span($"API Key: {Model.ApiKey}")
                        .FontSize(14)
                        .FontColor(darkBlue);
                });
            }
            if (!string.IsNullOrEmpty(Model.DownloadedOn))
            {
                column.Item().Text(text =>
                {
                    text.Span($"Downloaded On: {Model.DownloadedOn}")
                        .FontSize(14)
                        .FontColor(darkBlue);
                });
            }
        });
    }

    IContainer CellStyle(IContainer container, string color)
    {
        return container.Padding(5)
            .BorderBottom(1)
            .BorderColor(color);
    }

    void ComposeConversation(IContainer container)
    {
        var darkBlue = "#181851";
        container.Column(column =>
        {
            column.Spacing(15);
            column.Item().Text(text =>
            {
                text.Span("Chat History")
                    .FontSize(18)
                    .Bold()
                    .FontColor(darkBlue);
            });
            foreach (var entry in Model.Conversation)
            {
                column.Item().Element(element =>
                {
                    element.Padding(10)
                        .Border(1)
                        .BorderColor(darkBlue)
                        .Column(inner =>
                        {
                            inner.Spacing(8);
                            if (!string.IsNullOrWhiteSpace(entry.Label))
                            {
                                inner.Item().Text(text =>
                                {
                                    text.Span(entry.Label)
                                        .FontSize(14)
                                        .Bold()
                                        .FontColor(darkBlue);
                                });
                            }
                            if (!string.IsNullOrWhiteSpace(entry.User))
                            {
                                inner.Item().Text(text =>
                                {
                                    text.Span("User:")
                                        .FontSize(12)
                                        .Bold()
                                        .FontColor(darkBlue);
                                });
                                inner.Item().Text(text =>
                                {
                                    text.Span(entry.User)
                                        .FontSize(12)
                                        .FontColor(darkBlue);
                                });
                            }
                            if (!string.IsNullOrWhiteSpace(entry.Assistant))
                            {
                                inner.Item().Text(text =>
                                {
                                    text.Span("Assistant:")
                                        .FontSize(12)
                                        .Bold()
                                        .FontColor(darkBlue);
                                });
                                inner.Item().Text(text =>
                                {
                                    text.Span(entry.Assistant)
                                        .FontSize(12)
                                        .FontColor(darkBlue);
                                });
                            }
                        });
                });
            }
        });
    }
}