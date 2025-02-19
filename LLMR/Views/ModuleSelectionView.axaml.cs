using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace LLMR.Views;

public partial class ModuleSelectionView : Window
{
    public ModuleSelectionView()
    {
        InitializeComponent();
        var window = this.GetVisualRoot() as Window;
        if (window != null)
        {
            DataContext = window.DataContext;
        }
    }
    
    protected override void OnAttachedToVisualTree(Avalonia.VisualTree.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (this.GetVisualRoot() is Window window)
        {
            DataContext = window.DataContext;
        }
    }
    
}