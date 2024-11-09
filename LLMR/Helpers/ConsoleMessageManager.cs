using System;
using Avalonia.Media;
using Avalonia.Threading;
using LLMR.Models;

namespace LLMR.Helpers;

public static class ConsoleMessageManager
{
    public static event Action<ConsoleMessage>? OnConsoleMessageCreated;

    public static void LogInfo(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() => CreateConsoleMessage(message, MessageType.Info));
    }

    public static void LogError(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() => CreateConsoleMessage(message, MessageType.Error));
    }

    public static void LogWarning(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() => CreateConsoleMessage(message, MessageType.Warning));
    }

    public static void LogDebug(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() => CreateConsoleMessage(message, MessageType.Debug));
    }

    public static void LogPythonStdOut(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() => CreateConsoleMessage(message, MessageType.PythonStdOut));
    }

    public static void LogPythonStdErr(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() => CreateConsoleMessage(message, MessageType.PythonStdErr));
    }

    public static void LogPathUsage(string path)
    {
        Dispatcher.UIThread.InvokeAsync(() => CreateConsoleMessage($"Path: {path}", MessageType.Path));
    }


    public static ConsoleMessage CreateConsoleMessage(string message, MessageType messageType)
    {
        var (processedMessage, color) = ProcessMessage(message, messageType);
        var consoleMessage = new ConsoleMessage
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            Text = processedMessage,
            Color = color
        };

        OnConsoleMessageCreated?.Invoke(consoleMessage);
        return consoleMessage;
    }

    private static (string, SolidColorBrush) ProcessMessage(string message, MessageType messageType)
    {
        SolidColorBrush color;
        string processedMessage = message;

        switch (messageType)
        {
            case MessageType.Info:
                color = new SolidColorBrush(Colors.ForestGreen);
                break;
            case MessageType.Error:
                color = new SolidColorBrush(Colors.DarkRed);
                break;
            case MessageType.Warning:
                color = new SolidColorBrush(Colors.Tomato);
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
                color = new SolidColorBrush(Colors.Gray);
                break;
            case MessageType.Path:
                color = new SolidColorBrush(Color.Parse("#A52A2A")); // Brown color
                break;
            default:
                color = new SolidColorBrush(Colors.Black);
                break;
        }

        return (processedMessage, color);
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
    Path
}