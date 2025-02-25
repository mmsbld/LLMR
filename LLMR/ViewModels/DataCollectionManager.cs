using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;
using QuestPDF.Fluent;
using LLMR.Helpers;
using LLMR.Services;
using System.Reactive;
using Avalonia.Controls.ApplicationLifetimes;
using LLMR.Model.ChatHistoryManager;
using LLMR.Model.UserSettings;

namespace LLMR.ViewModels;

public class DataCollectionManager
{
    public ChatHistoryCollection ChatHistoryCollection { get; }
    public ReactiveCommand<Unit, Unit> AddFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveItemCommand { get; }
    public ReactiveCommand<Unit, Unit> RenameItemCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadAllFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadSelectedAsPdfCommand { get; }
    public ReactiveCommand<Unit, Unit> SortByDateCommand { get; }
    private readonly DialogService _dialogService;
    public DataCollectionManager(DialogService dialogService)
    {
        _dialogService = dialogService;
        ChatHistoryCollection = new ChatHistoryCollection();
        ChatHistoryCollection.ConsoleMessageOccurred += OnConsoleMessageOccurred;
        ChatHistoryCollection.ExceptionOccurred += OnExceptionOccurred;
        LoadChatHistories();
        AddFolderCommand = ReactiveCommand.CreateFromTask(AddFolderAsync);
        RemoveItemCommand = ReactiveCommand.Create(RemoveItem);
        RenameItemCommand = ReactiveCommand.CreateFromTask(RenameItemAsync);
        DownloadAllFilesCommand = ReactiveCommand.CreateFromTask(DownloadAllFilesAsync);
        DownloadSelectedAsPdfCommand = ReactiveCommand.CreateFromTask(DownloadSelectedAsPdfAsync);
        SortByDateCommand = ReactiveCommand.Create(SortByDate);
    }

    private void OnConsoleMessageOccurred(object? sender, string message)
    {
        ConsoleMessageManager.LogInfo(message);
    }

    private void OnExceptionOccurred(object? sender, string message)
    {
        ConsoleMessageManager.LogError(message);
    }

    public void LoadChatHistories()
    {
        ChatHistoryCollection.LoadFiles();
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
            ChatHistoryCollection.RemoveItem(ChatHistoryCollection.SelectedItem);
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
            ChatHistoryCollection.RenameItem(ChatHistoryCollection.SelectedItem, newName);
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
                await _dialogService.ShowMessageAsync("Exported backup successfully", "All chat histories were successfully exported.");
            }
            else
            {
                ConsoleMessageManager.LogWarning("No directory selected.");
            }
        }
        catch (Exception ex)
        {
            ConsoleMessageManager.LogError($"Error during PDF export: {ex.Message}");
            await _dialogService.ShowMessageAsync("PDF export was not successful", $"There was an error: {ex.Message}");
        }
        return Unit.Default;
    }

    private bool success;
    private async Task<Unit> DownloadSelectedAsPdfAsync()
    {
        try
        {
            if (!(ChatHistoryCollection.SelectedItem is ChatHistoryFile file) ||
                string.IsNullOrEmpty(file.Filename))
            {
                ConsoleMessageManager.LogWarning("No chat history selected.");
                return Unit.Default;
            }
            var topLevel = GetTopLevel();
            var saveFile = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export the chosen chat history as a PDF file.",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType("PDF Files") { Patterns = new[] { "*.pdf" } }
                },
                SuggestedFileName = $"{Path.GetFileNameWithoutExtension(file.Filename)}.pdf"
            });
            if (saveFile != null)
            {
                var pdfPath = saveFile.Path.LocalPath;
                ConsoleMessageManager.LogInfo($"PDF is saved under {pdfPath}.");
                try
                {
                    GeneratePdf(pdfPath);
                }
                catch (Exception e)
                {
                    success = false;
                    ConsoleMessageManager.LogError("Error generating PDF: " + e.Message);
                    return Unit.Default;
                }
            }
            else
            {
                success = false;
                ConsoleMessageManager.LogWarning("No chat history selected.");
                return Unit.Default;
            }
        }
        catch (Exception ex)
        {
            success = false;
            ConsoleMessageManager.LogError($"Error exporting PDF: {ex.Message}");
            return Unit.Default;
        }
        return Unit.Default;
    }

    private void GeneratePdf(string pdfPath)
    {
        try
        {
            var pdfExportSettings = new PdfExportSettings();
            var pdf = new ChatHistoryDocument(ChatHistoryCollection, pdfExportSettings);
            pdf.GeneratePdf(pdfPath);
            ConsoleMessageManager.LogInfo($"PDF generated at {pdfPath}.");
        }
        catch (Exception ex)
        {
            ConsoleMessageManager.LogError($"Error generating PDF: {ex.Message}");
            _dialogService.ShowMessageAsync("Export not successful", $"There was an error: {ex.Message}");
            return;
        }
        _dialogService.ShowMessageAsync("Export successful", "The chosen chat history was successfully exported as PDF.");
    }

    private TopLevel GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: not null } desktop)
            return desktop.MainWindow;
        throw new InvalidOperationException("<DataCollectionManager> Unable to get the main window.");
    }

    private void SortByDate()
    {
        ChatHistoryCollection.SortFilesByDate();
        ConsoleMessageManager.LogInfo("Files sorted by date.");
    }

    public void MoveFileToFolder(ChatHistoryFile file, ChatHistoryCategory targetCategory)
    {
        try
        {
            ChatHistoryCollection.MoveFileToFolder(file, targetCategory);
            ConsoleMessageManager.LogInfo($"Moved file {file.Filename} to folder {targetCategory.Name}.");
        }
        catch (Exception ex)
        {
            ConsoleMessageManager.LogError($"Error moving file: {ex.Message}");
        }
    }

    public void MoveFolderToFolder(ChatHistoryCategory sourceFolder, ChatHistoryCategory targetFolder)
    {
        if (sourceFolder.ParentCategory == null)
            throw new InvalidOperationException("Cannot move the root folder.");
        var newPath = Path.Combine(targetFolder.FullPath, sourceFolder.Name);
        if (Directory.Exists(newPath))
            throw new IOException($"A folder with name {sourceFolder.Name} already exists in {targetFolder.Name}.");
        Directory.Move(sourceFolder.FullPath, newPath);
        sourceFolder.FullPath = newPath;
        sourceFolder.ParentCategory = targetFolder;
        ConsoleMessageManager.LogInfo($"Moved folder {sourceFolder.Name} to folder {targetFolder.Name}.");
        LoadChatHistories();
    }
}