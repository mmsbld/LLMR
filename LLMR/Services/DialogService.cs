using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using LLMR.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;

namespace LLMR.Services;

public class DialogService
{
    public async Task<string> PromptUserAsync(string message)
    {
        var inputDialog = new InputDialog
        {
            ViewModel =
            {
                Title = "Input Required",
                Message = message
            }
        };

        var result = await inputDialog.ShowDialog<string>(GetMainWindow());
        return result;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var messageBoxCustomWindow = MessageBoxManager.GetMessageBoxCustom(
            new MessageBoxCustomParams
            {
                ContentTitle = title,
                ContentMessage = message,
                ButtonDefinitions = new[]
                {
                    new ButtonDefinition { Name = "OK" }
                },
                Icon = MsBox.Avalonia.Enums.Icon.Info,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                SizeToContent = SizeToContent.WidthAndHeight
            });

        await messageBoxCustomWindow.ShowWindowDialogAsync(GetMainWindow());
    }

    private Window GetMainWindow()
    {
        var appCurrent = Avalonia.Application.Current;
        if (appCurrent is null)
            throw new System.NullReferenceException("Application.Current is null.");
        var mainWindow = (appCurrent.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (mainWindow != null)
            return mainWindow;
        throw new System.NullReferenceException("mainWindow is null.");
    }
}