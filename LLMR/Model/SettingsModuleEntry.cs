using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;

namespace LLMR.Model;

public class SettingsModuleEntry : ReactiveObject
{
    private string _iconPath;
    public string IconPath
    {
        get => _iconPath;
        set => this.RaiseAndSetIfChanged(ref _iconPath, value);
    }
    
    public Bitmap? Icon
    {
        get
        {
            try
            {
                return new Bitmap(AssetLoader.Open(new Uri(IconPath)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load image: {ex.Message}");
                return null;
            }
        }
    }


    private string _title;
    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    private string _description;
    public string Description
    {
        get => _description;
        set => this.RaiseAndSetIfChanged(ref _description, value);
    }
}