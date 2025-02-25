using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using LLMR.Model.ChatHistoryManager;
using LLMR.ViewModels;

namespace LLMR.Behaviors;

public class NodesTreeViewDropHandler : DropHandlerBase
{
    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        // Console.WriteLine("Validate called.");
        // Console.WriteLine("sourceContext: " + (sourceContext?.GetType().Name ?? "null"));
        // Console.WriteLine("targetContext: " + (targetContext?.GetType().Name ?? "null"));

        if (targetContext is ChatHistoryCategory &&
            (sourceContext is ChatHistoryFile || sourceContext is ChatHistoryCategory))
        {
            // Console.WriteLine("Validation passes, DragEffects: " + e.DragEffects);
            return e.DragEffects.HasFlag(DragDropEffects.Move);
        }
        // Console.WriteLine("Validation fails.");
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        // Console.WriteLine("Execute called.");
        // Attempt to retrieve the MainWindowViewModel from the sender.
        MainWindowViewModel? vm = null;
        if (sender is TreeView treeView)
        {
            vm = treeView.DataContext as MainWindowViewModel;
        }
        else if (sender is TreeViewItem tvi)
        {
            vm = tvi.GetVisualAncestors().OfType<TreeView>().FirstOrDefault()?.DataContext as MainWindowViewModel;
        }
        if (vm == null)
        {
            // Console.WriteLine("Unable to retrieve MainWindowViewModel.");
            return false;
        }

        if (sourceContext is ChatHistoryFile file && targetContext is ChatHistoryCategory folder)
        {
            // Console.WriteLine($"Moving file '{file.Filename}' to folder '{folder.Name}'.");
            vm.DataCollectionManager.MoveFileToFolder(file, folder);
            return true;
        }
        else if (sourceContext is ChatHistoryCategory sourceFolder && targetContext is ChatHistoryCategory targetFolder)
        {
            if (sourceFolder.ParentCategory == null)
            {
                // Console.WriteLine("Attempted to move root folder; aborting.");
                return false;
            }
            // Console.WriteLine($"Moving folder '{sourceFolder.Name}' to folder '{targetFolder.Name}'.");
            vm.DataCollectionManager.MoveFolderToFolder(sourceFolder, targetFolder);
            return true;
        }
        // Console.WriteLine("Execute did not match any condition.");
        return false;
    }
}