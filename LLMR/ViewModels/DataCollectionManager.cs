using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ReactiveUI;
using QuestPDF.Fluent;
using LLMR.Model.ChatHistoryManager;
using LLMR.Services;
using LLMR.Helpers;
using Unit = System.Reactive.Unit;

namespace LLMR.ViewModels;

/// <summary>
/// Encapsulates all data collection functionality – managing the chat history,
/// adding/removing/renaming folders and items, and handling PDF exports (the PDF part for now, at least).
/// </summary>
public class DataCollectionManager
{
    public ChatHistoryCollection ChatHistoryCollection { get; }
        
    public ReactiveCommand<Unit, Unit> AddFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveItemCommand { get; }
    public ReactiveCommand<Unit, Unit> RenameItemCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadAllFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadSelectedAsPdfCommand { get; }

    private readonly DialogService _dialogService;

    public DataCollectionManager(DialogService dialogService)
    {
        _dialogService = dialogService;

        // Initialize chat history collection & subscribe to events
        ChatHistoryCollection = new ChatHistoryCollection();
        ChatHistoryCollection.ConsoleMessageOccurred += OnConsoleMessageOccurred;
        ChatHistoryCollection.ExceptionOccurred += OnExceptionOccurred;

        LoadChatHistories();

        // Initialize commands
        AddFolderCommand = ReactiveCommand.CreateFromTask(AddFolderAsync);
        RemoveItemCommand = ReactiveCommand.Create(RemoveItem);
        RenameItemCommand = ReactiveCommand.CreateFromTask(RenameItemAsync);
        DownloadAllFilesCommand = ReactiveCommand.CreateFromTask(DownloadAllFilesAsync);
        DownloadSelectedAsPdfCommand = ReactiveCommand.CreateFromTask(DownloadSelectedAsPdfAsync);
    }

    private void OnConsoleMessageOccurred(object? sender, string message)
    {
        // Could also be possible: raising an event or calling a callback to inform MainWindowViewModel
        // (directly logging also possible for simplicity:)
        ConsoleMessageManager.LogInfo(message);
    }

    private void OnExceptionOccurred(object? sender, string message)
    {
        ConsoleMessageManager.LogError(message);
    }

    public void LoadChatHistories()
    {
        //var directoryPath = PathManager.Combine(PathManager.GetBaseDirectory(), "Scripts", "chat_histories");
        ChatHistoryCollection.LoadFiles();
        
        //     var directoryPath = PathManager.Combine(PathManager.GetBaseDirectory(), "Scripts", "chat_histories");
        //     ChatHistoryCollection.LoadFiles(directoryPath);
    }

    private async Task<Unit> AddFolderAsync()
    {
        var folderName = await _dialogService.PromptUserAsync("Enter the name of the new folder:");
        if (string.IsNullOrWhiteSpace(folderName))
        {
            ConsoleMessageManager.LogInfo("Folder creation canceled by the user.");
            return Unit.Default;
        }

        try
        {
            ChatHistoryCollection.AddFolder(folderName);
            ConsoleMessageManager.LogInfo($"Folder '{folderName}' added successfully.");
        }
        catch (Exception ex)
        {
            ConsoleMessageManager.LogError($"Error adding folder: {ex.Message}");
        }

        return Unit.Default;
    }

    private void RemoveItem()
    {
        try
        {
            ChatHistoryCollection.RemoveItem(ChatHistoryCollection.SelectedFile);
            ConsoleMessageManager.LogInfo("Item removed successfully.");
        }
        catch (Exception ex)
        {
            ConsoleMessageManager.LogError($"Error removing item: {ex.Message}");
        }
    }

    private async Task<Unit> RenameItemAsync()
    {
        var newName = await _dialogService.PromptUserAsync("Enter the new name:");
        if (string.IsNullOrWhiteSpace(newName))
        {
            ConsoleMessageManager.LogInfo("Rename operation canceled by the user.");
            return Unit.Default;
        }

        try
        {
            ChatHistoryCollection.RenameItem(ChatHistoryCollection.SelectedFile, newName);
            ConsoleMessageManager.LogInfo("Item renamed successfully.");
        }
        catch (Exception ex)
        {
            ConsoleMessageManager.LogError($"Error renaming item: {ex.Message}");
        }

        return Unit.Default;
    }

    private async Task<Unit> DownloadAllFilesAsync()
    {
        try
        {
            var topLevel = GetTopLevel();
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose a folder to copy all chat histories to."
            });

            if (folders.Count > 0)
            {
                var targetDirectory = folders[0].Path.LocalPath;
                var sourceDirectory = PathManager.Combine(PathManager.GetBaseDirectory(), "Scripts", "chat_histories");

                foreach (var file in Directory.GetFiles(sourceDirectory, "*.json"))
                {
                    var destFile = Path.Combine(targetDirectory, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                    ConsoleMessageManager.LogInfo($"Copied {file} to {destFile}.");
                }

                await _dialogService.ShowMessageAsync("Download successful", "All JSON-files were successfully downloaded.");
            }
            else
            {
                ConsoleMessageManager.LogWarning("No directory selected.");
            }
        }
        catch (Exception ex)
        {
            ConsoleMessageManager.LogError($"Error during download: {ex.Message}");
            await _dialogService.ShowMessageAsync("Download was not successful", $"There was an error: {ex.Message}");
        }

        return Unit.Default;
    }

    private async Task<Unit> DownloadSelectedAsPdfAsync()
    {
        try
        {
            if (ChatHistoryCollection.SelectedFile == null ||
                string.IsNullOrEmpty(ChatHistoryCollection.SelectedFile.Filename))
            {
                await _dialogService.ShowMessageAsync("No file chosen", "Please select a chat history to download.");
                ConsoleMessageManager.LogWarning("No chat history selected.");
                return Unit.Default;
            }

            var topLevel = GetTopLevel();
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export the chosen chat history as a PDF file.",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType("PDF Files") { Patterns = new[] { "*.pdf" } }
                },
                SuggestedFileName = $"{Path.GetFileNameWithoutExtension(ChatHistoryCollection.SelectedFile.Filename)}.pdf"
            });

            if (file != null)
            {
                var pdfPath = file.Path.LocalPath;
                ConsoleMessageManager.LogInfo($"PDF is saved under {pdfPath}.");
                GeneratePdf(pdfPath);
                await _dialogService.ShowMessageAsync("Export successful", "The chosen chat history was successfully exported as PDF.");
            }
            else
            {
                ConsoleMessageManager.LogWarning("No chat history selected.");
            }
        }
        catch (Exception ex)
        {
            ConsoleMessageManager.LogError($"Error exporting PDF: {ex.Message}");
            await _dialogService.ShowMessageAsync("Download not successful", $"There was an error: {ex.Message}");
        }

        return Unit.Default;
    }

    private void GeneratePdf(string pdfPath)
    {
        try
        {
            var pdf = new ChatHistoryDocument(ChatHistoryCollection);
            pdf.GeneratePdf(pdfPath);
            ConsoleMessageManager.LogInfo($"PDF generated at {pdfPath}.");
        }
        catch (Exception ex)
        {
            ConsoleMessageManager.LogError($"Error generating PDF: {ex.Message}");
        }
    }

    private TopLevel GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: not null } desktop)
            return desktop.MainWindow;
        throw new InvalidOperationException("<DataCollectionManager> Unable to get the main window.");
    }
}