using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LLMR.ViewModels;
using LLMR.Views;

namespace LLMR;

public partial class App : Application
{
    public static TopLevel TopLevel { get; private set; } //Note by Moe: not the mvvm/ReactiveUI way of doing this! Compare: https://github.com/AvaloniaUI/Avalonia/discussions/13599
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            TopLevel = TopLevel.GetTopLevel(desktop.MainWindow); //Note by Moe: still... not mvvm conform!
        }

        base.OnFrameworkInitializationCompleted();
    }
}