using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LLMR.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}