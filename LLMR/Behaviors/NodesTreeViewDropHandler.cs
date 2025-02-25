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
            // allow drop only if:
            // - sourceContext is a ChatHistoryFile (the file being dragged)
            // - targetContext is a ChatHistoryCategory (the folder being dropped onto)
            // - and the drag effect supports moving.
            if (sourceContext is ChatHistoryFile && targetContext is ChatHistoryCategory)
            {
                return e.DragEffects.HasFlag(DragDropEffects.Move);
            }
            return false;
        }

        public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
        {
            // when the drop is executed, if the source is a file and the target is a folder,
            // attempt to move the file by calling the MoveFileToFolder method on the MainWindowViewModel.
            if (sourceContext is ChatHistoryFile file && targetContext is ChatHistoryCategory folder)
            {
                // Try to retrieve the main view model from the TreeView's DataContext.
                if (sender is TreeView treeView && treeView.DataContext is MainWindowViewModel vm)
                {
                    vm.DataCollectionManager.MoveFileToFolder(file, folder);
                    return true;
                }
            }
            return false;
        }
    }
}