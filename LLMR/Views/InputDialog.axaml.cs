using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LLMR.ViewModels;

namespace LLMR.Views;

public partial class InputDialog : Window
{
    public InputDialogViewModel ViewModel { get; }

    public InputDialog()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        ViewModel = new InputDialogViewModel();
        DataContext = ViewModel;

        ViewModel.CloseRequested += (sender, result) =>
        {
            Close(result);
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
