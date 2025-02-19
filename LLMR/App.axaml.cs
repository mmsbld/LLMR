using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LLMR.Helpers;
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
        InitializeDataDirectories();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new ShellView
            {
                DataContext = new ShellViewModel(),
            };
            TopLevel = TopLevel.GetTopLevel(desktop.MainWindow) ?? throw new InvalidOperationException("<App.ax> TopLevel MainWindow of desktop is null."); //Note by Moe: still... not mvvm conform!
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private void InitializeDataDirectories()
    {
        string baseDataDir = AppDataPath.GetBaseDataDirectory();
        string chatHistoriesDir = Path.Combine(baseDataDir, "chat_histories");

        if (!Directory.Exists(chatHistoriesDir))
        {
            Directory.CreateDirectory(chatHistoriesDir);
        }
    }
}