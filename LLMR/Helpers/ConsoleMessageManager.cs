using System;
using Avalonia.Media;
using Avalonia.Threading;
using LLMR.Model;

namespace LLMR.Helpers;

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
        SolidColorBrush color;
        var processedMessage = message;

        switch (messageType)
        {
            case MessageType.Info:
                color = new SolidColorBrush(Colors.Lime);
                break;
            case MessageType.Error:
                color = new SolidColorBrush(Colors.Red);
                break;
            case MessageType.Warning:
                color = new SolidColorBrush(Colors.Yellow);
                break;
            case MessageType.PythonStdOut:
                color = new SolidColorBrush(Colors.LightGray);
                processedMessage = message.Replace("<PES stdout>", "").Trim();
                break;
            case MessageType.PythonStdErr:
                color = new SolidColorBrush(Colors.DarkGray);
                processedMessage = message.Replace("<PES stderr>", "").Trim();
                break;
            case MessageType.Debug:
                color = new SolidColorBrush(Colors.Magenta);
                break;
            case MessageType.Path:
                color = new SolidColorBrush(Colors.Turquoise);
                break;
            case MessageType.SystemInfo:
                color = new SolidColorBrush(Colors.Cyan);
                break;
            case MessageType.NetworkWarning:
                color = new SolidColorBrush(Colors.Purple);
                break;
            case MessageType.Welcome:
            case MessageType.Goodbye:
            case MessageType.Banner:
                color = new SolidColorBrush(Colors.Cyan);
                break;
            default:
                color = new SolidColorBrush(Colors.White);
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