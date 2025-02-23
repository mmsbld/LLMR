using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LLMR.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
    }
    
    public void OnLinkClicked(object sender, PointerPressedEventArgs e)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "https://github.com/mmsbld/LLMR",
            UseShellExecute = true
        };
        Process.Start(psi);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}