using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Input.Platform;
using Avalonia.Controls.ApplicationLifetimes;

namespace LLMR.Helpers;

public class Clipboard {
    
    public static IClipboard Get() {

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }) {
            return window.Clipboard!;

        }
        
        
        else if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: { } mainView }) {
            var visualRoot = mainView.GetVisualRoot();
            if (visualRoot is TopLevel topLevel) {
                return topLevel.Clipboard!;
            }
        }

        return null!;
    }
}

// note from Moe: Code from O.Henriksson (https://stackoverflow.com/questions/76855551/how-do-i-copy-text-to-clipboard-in-avalonia)
// sample use:

// var clipboard = Clipboard.Get();
// if (clipboard != null) {
//     await clipboard.SetTextAsync("This text should now be on the clipboard");
// }