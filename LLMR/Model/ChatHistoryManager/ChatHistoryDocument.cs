using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using LLMR.Model.UserSettings;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace LLMR.Model.ChatHistoryManager
{
    public class ChatHistoryDocument : IDocument
    {
        // logo as byte array
        public static byte[] LogoBytes { get; } = File.ReadAllBytes("Assets/logo/logo_full.png");
        public ChatHistoryCollection Model { get; }
        public PdfExportSettings ExportSettings { get; }

        public ChatHistoryDocument(ChatHistoryCollection model, PdfExportSettings exportSettings)
        {
            Model = model;
            ExportSettings = exportSettings;
        }

        public DocumentMetadata GetMetadata() => new DocumentMetadata
        {
            Title = "Chat History - LLMRunner (v0.6)",
            Author = "LLMRunner - Moritz Seibold",
            Subject = "Chat History LLMRunner (pre-alpha)",
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
                page.Margin(50);

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

            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    // LOGO, version, and file
                    row.ConstantItem(230) // WIDTH of left column (logo, version & file)
                        .Column(left =>
                        {
                            left.Item().Image(LogoBytes).FitArea(); // logo
                            left.Item().Text("Version 0.6")         // under logo
                                .FontSize(14).FontColor(darkBlue);
                
                            if (Model.SelectedItem is ChatHistoryFile f)
                            {
                                left.Item().Text($"File: {f.Filename}") // (under "Version..." and so on)
                                    .FontSize(12).FontColor(darkBlue);
                            }
                        });

                    // BOX (right side) 
                    row.RelativeItem()
                       .AlignRight()
                       .MinWidth(160).MaxWidth(160) // WIDTH
                       .Border(1)
                       .BorderColor(darkBlue)
                       .Padding(5)
                       .Column(rcol =>
                       {
                           // app info and current date/time details asin console
                           var assemblyVersion = System.Reflection.Assembly
                               .GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
                           var osName = GetOperatingSystemName();
                           var osDescription = RuntimeInformation.OSDescription;
                           var currentDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                           var timezone = TimeZoneInfo.Local.DisplayName;
                           var appInfo = $"LLMRunner v{assemblyVersion} running on {osName} ({osDescription}).";
                           var dateTimeString = $"Current time: {currentDate} ({timezone}).";
                           
                           var combinedBoxText = appInfo + "\n" + dateTimeString;
                           combinedBoxText = TruncateWithEllipsis(combinedBoxText, 230);
                           
                           rcol.Item().Text(combinedBoxText)
                               .FontSize(9)
                               .FontColor(darkBlue);
                       });
                });
                
                col.Item().Height(20);

                // "Created: ..." & line below (right aligned)
                col.Item().AlignRight().Text(t =>
                {
                    t.Span("Created: ").FontSize(10).FontColor(darkBlue);
                    t.Span(DateTime.Now.ToString("f", CultureInfo.CurrentCulture))
                        .FontSize(10).FontColor(darkBlue);
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
                column.Item().Element(ComposeDetails);
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
                    if (ExportSettings.ShowModelName)
                    {
                        column.Item().Text(text =>
                        {
                            text.Span($"Model: {Model.Settings.SelectedModel}")
                                .FontSize(14)
                                .FontColor(darkBlue);
                        });
                    }
                    if (ExportSettings.ShowModelParameters &&
                        Model.Settings.Parameters != null &&
                        Model.Settings.Parameters.Any())
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
                                    .Text(txt => txt.Span(param.Name));
                                table.Cell().Element(cell => CellStyle(cell, darkBlue))
                                    .Text(txt => txt.Span(param.Value?.ToString() ?? string.Empty));
                            }
                        });
                    }
                }
                if (ExportSettings.ShowApiKey && !string.IsNullOrEmpty(Model.ApiKey))
                {
                    column.Item().Text(text =>
                    {
                        text.Span($"API Key: {MaskApiKey(Model.ApiKey)}")
                            .FontSize(14)
                            .FontColor(darkBlue);
                    });
                }
                if (ExportSettings.ShowDownloadedOn && !string.IsNullOrEmpty(Model.DownloadedOn))
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

        string MaskApiKey(string? key)
        {
            if (string.IsNullOrEmpty(key))
                return "";
            if (ExportSettings.ShowFullApiKey)
                return key;
            if (key.Length <= 4)
                return new string('*', key.Length);
            return new string('*', key.Length - 4) + key.Substring(key.Length - 4);
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
                        element.Border(1)
                            .BorderColor(darkBlue)
                            .Padding(10)
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

        static string GetOperatingSystemName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macOS";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
            return "Unknown OS";
        }

        // truncate string
        static string TruncateWithEllipsis(string input, int maxChars)
        {
            if (input.Length > maxChars)
                return input.Substring(0, maxChars) + "...";
            return input;
        }
    }
}
