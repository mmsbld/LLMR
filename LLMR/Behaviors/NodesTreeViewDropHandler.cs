using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.DragAndDrop;
using LLMR.Model.ChatHistoryManager;
using LLMR.ViewModels;

namespace LLMR.Behaviors
{
    public class NodesTreeViewDropHandler : DropHandlerBase
    {
        public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
        {
            if (targetContext is ChatHistoryCategory &&
                (sourceContext is ChatHistoryFile || sourceContext is ChatHistoryCategory))
            {
                return e.DragEffects.HasFlag(DragDropEffects.Move);
            }
            return false;
        }
        public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
        {
            if (sourceContext is ChatHistoryFile file && targetContext is ChatHistoryCategory folder)
            {
                if (sender is TreeView treeView && treeView.DataContext is MainWindowViewModel vm)
                {
                    vm.DataCollectionManager.MoveFileToFolder(file, folder);
                    return true;
                }
            }
            else if (sourceContext is ChatHistoryCategory sourceFolder && targetContext is ChatHistoryCategory targetFolder)
            {
                if (sourceFolder.ParentCategory == null)
                    return false;
                if (sender is TreeView treeView && treeView.DataContext is MainWindowViewModel vm)
                {
                    vm.DataCollectionManager.MoveFolderToFolder(sourceFolder, targetFolder);
                    return true;
                }
            }
            return false;
        }
    }
}