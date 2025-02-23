using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;

namespace LLMR.Views.Tabs;

public partial class LoginTab : UserControl
{
    public LoginTab()
    {
        InitializeComponent();
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

}