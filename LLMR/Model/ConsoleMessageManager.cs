using System;
using Avalonia.Media;
using Avalonia.Threading;

namespace LLMR.Model;

public static class ConsoleMessageManager
{
    public static event Action<ConsoleMessage>? OnConsoleMessageCreated;
        
    public static void PrintSystemInfo() => Dispatcher.UIThread.InvokeAsync(CreateSystemInfoMessage);
    public static void PrintNetworkWarning() => Dispatcher.UIThread.InvokeAsync(CreateNetworkWarningMessage);
    public static void PrintWelcomeMessage() => Dispatcher.UIThread.InvokeAsync(CreateWelcomeMessage);
    public static void PrintGoodbyeMessage() => Dispatcher.UIThread.InvokeAsync(CreateGoodbyeMessage);
    public static void PrintCustomBanner(string message) => Dispatcher.UIThread.InvokeAsync(() => CreateBannerMessage(message));
    public static void LogInfo(string message) => Dispatcher.UIThread.InvokeAsync(() => CreateConsoleMessage(message, MessageType.Info));
    public static void LogError(string message) => Dispatcher.UIThread.InvokeAsync(() => CreateConsoleMessage(message, MessageType.Error));
    public static void LogWarning(string message) => Dispatcher.UIThread.InvokeAsync(() => CreateConsoleMessage(message, MessageType.Warning));
    public static void LogDebug(string message) => Dispatcher.UIThread.InvokeAsync(() => CreateConsoleMessage(message, MessageType.Debug));
    public static void LogPythonStdOut(string message) => Dispatcher.UIThread.InvokeAsync(() => CreateConsoleMessage(message, MessageType.PythonStdOut));
    public static void LogPythonStdErr(string message) => Dispatcher.UIThread.InvokeAsync(() => CreateConsoleMessage(message, MessageType.PythonStdErr));
    public static void LogPathUsage(string path) => Dispatcher.UIThread.InvokeAsync(() => CreatePathMessage(path));

    // Stylized message creators
    private static void CreateSystemInfoMessage()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        var osName = GetOperatingSystemName();
        var osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        var currentDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        var timezone = TimeZoneInfo.Local.DisplayName;

        var appInfo = $"LLMRunner v{version} running on {osName} ({osDescription}).";
        var dateTime = $"Current time: {currentDate} ({timezone}).";

        var messages = new[] { appInfo, dateTime };

        CreateStylizedMessage(messages, MessageType.SystemInfo);
    }

    private static void CreateNetworkWarningMessage()
    {
        var warnings = new[]
        {
            "Please ensure your connection is stable and all required ports are open.",
            "In public networks (e.g., schools or universities), some ports may be restricted.",
            "Consider using a private network, such as a mobile hotspot for running LLMR.",
            "The client interface is not affected; clients can use public networks.",
            "Chat histories are saved locally even when running in a private network."
        };

        CreateStylizedMessage(warnings, MessageType.NetworkWarning);
    }

    private static void CreateWelcomeMessage()
    {
        var messages = new[]
        {
            "Welcome to LLMRunner!",
            "LLM Research in Math Educ."
        };

        CreateStylizedMessage(messages, MessageType.Welcome);
    }

    private static void CreateGoodbyeMessage()
    {
        var messages = new[]
        {
            "Thank you for using LLMRunner!",
            "Shutting down..."
        };

        CreateStylizedMessage(messages, MessageType.Goodbye);
    }

    private static void CreateBannerMessage(string message)
    {
        var messages = new[] { message };
        CreateStylizedMessage(messages, MessageType.Banner);
    }

    private static void CreateStylizedMessage(string[] lines, MessageType messageType)
    {
        var borderChar = messageType == MessageType.Path ? '\u2731' : '\u2732';
        const int totalWidth = 70;

        var borderLine = new string(borderChar, totalWidth);

        CreateConsoleMessage(string.Empty, messageType);
        CreateConsoleMessage(borderLine, messageType);
            
        foreach (var line in lines)
        {
            CreateConsoleMessage(line, messageType);
        }

        CreateConsoleMessage(borderLine, messageType);
        CreateConsoleMessage(string.Empty, messageType);
    }

    private static void CreatePathMessage(string path)
    {
        var messages = new[] { path };
        CreateStylizedMessage(messages, MessageType.Path);
    }

    public static ConsoleMessage CreateConsoleMessage(string message, MessageType messageType)
    {
        var (processedMessage, color) = ProcessMessage(message, messageType);
        var consoleMessage = new ConsoleMessage
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            Text = processedMessage,
            Color = color,
            IsBold = ShouldBeBold(messageType)
        };

        OnConsoleMessageCreated?.Invoke(consoleMessage);
        return consoleMessage;
    }

    private static bool ShouldBeBold(MessageType messageType)
    {
        return messageType == MessageType.Path || messageType == MessageType.NetworkWarning || messageType == MessageType.Welcome;
    }

    private static (string, SolidColorBrush) ProcessMessage(string message, MessageType messageType)
    {
        var isDarkMode = Avalonia.Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        SolidColorBrush color;
        var processedMessage = message;

        switch (messageType)
        {
            case MessageType.Info:
                // Dark: Lime, Light: ForestGreen
                color = isDarkMode ? new SolidColorBrush(Colors.Lime) : new SolidColorBrush(Colors.ForestGreen);
                break;
            case MessageType.Error:
                // Dark: Red, Light: DarkRed
                color = isDarkMode ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.DarkRed);
                break;
            case MessageType.Warning:
                // Dark: Yellow, Light: DarkGoldenrod
                color = isDarkMode ? new SolidColorBrush(Colors.Yellow) : new SolidColorBrush(Colors.DarkGoldenrod);
                break;
            case MessageType.PythonStdOut:
                // Dark: LightGray, Light: Gray
                color = isDarkMode ? new SolidColorBrush(Colors.LightGray) : new SolidColorBrush(Colors.Gray);
                processedMessage = message.Replace("<PES stdout>", "").Trim();
                break;
            case MessageType.PythonStdErr:
                // Dark: DarkGray, Light: DimGray
                color = isDarkMode ? new SolidColorBrush(Colors.DarkGray) : new SolidColorBrush(Colors.DimGray);
                processedMessage = message.Replace("<PES stderr>", "").Trim();
                break;
            case MessageType.Debug:
                // Dark: Magenta, Light: DarkMagenta
                color = isDarkMode ? new SolidColorBrush(Colors.Magenta) : new SolidColorBrush(Colors.DarkMagenta);
                break;
            case MessageType.Path:
                // Dark: Turquoise, Light: CadetBlue
                color = isDarkMode ? new SolidColorBrush(Colors.Turquoise) : new SolidColorBrush(Colors.CadetBlue);
                break;
            case MessageType.SystemInfo:
                // Dark: Cyan, Light: DarkCyan
                color = isDarkMode ? new SolidColorBrush(Colors.Cyan) : new SolidColorBrush(Colors.DarkCyan);
                break;
            case MessageType.NetworkWarning:
                // Dark: Purple, Light: DarkSlateBlue
                color = isDarkMode ? new SolidColorBrush(Colors.Purple) : new SolidColorBrush(Colors.DarkSlateBlue);
                break;
            case MessageType.Welcome:
            case MessageType.Goodbye:
            case MessageType.Banner:
                // Dark: Cyan, Light: SteelBlue
                color = isDarkMode ? new SolidColorBrush(Colors.Cyan) : new SolidColorBrush(Colors.SteelBlue);
                break;
            default:
                // Dark: White, Light: Black
                color = isDarkMode ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Colors.Black);
                break;
        }

        return (processedMessage, color);
    }


    private static string GetOperatingSystemName()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)) return "macOS";
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)) return "Linux";
        return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "Windows" : "Unknown OS";
    }
}

public enum MessageType
{
    Info,
    Error,
    Warning,
    PythonStdOut,
    PythonStdErr,
    Debug,
    Path,
    SystemInfo,
    NetworkWarning,
    Welcome,
    Goodbye,
    Banner
}