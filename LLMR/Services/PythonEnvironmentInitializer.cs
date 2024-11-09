using System;
using System.Threading.Tasks;
using Avalonia.Media;
using LLMR.Helpers;
using LLMR.Models;
using ReactiveUI;

namespace LLMR.Services;

public class PythonEnvironmentInitializer : ReactiveObject
{
    private readonly PythonEnvironmentManager _pythonEnvironmentManager;

    public event Action<ConsoleMessage>? ConsoleMessageOccurred;
    public event Action<Exception>? ExceptionOccurred;

    public PythonEnvironmentInitializer()
    {
        _pythonEnvironmentManager = new PythonEnvironmentManager();
        _pythonEnvironmentManager.ConsoleMessageOccurred += OnConsoleMessageOccurred;
        _pythonEnvironmentManager.ExceptionOccurred += OnExceptionOccurred;
    }

    public async Task InitializePythonEnvironmentAsync()
    {
        try
        {
            await _pythonEnvironmentManager.EnsurePythonEnvironmentAsync();
        }
        catch (Exception ex)
        {
            ExceptionOccurred?.Invoke(ex);
        }
    }

    private void OnConsoleMessageOccurred(string message, SolidColorBrush color)
    {
        var consoleMessage = ConsoleMessageManager.CreateConsoleMessage(message, DetermineMessageType(color));
        ConsoleMessageOccurred?.Invoke(consoleMessage);
    }

    private MessageType DetermineMessageType(SolidColorBrush color)
    {
        if (color.Color == Colors.ForestGreen)
            return MessageType.Info;
        if (color.Color == Colors.DarkRed)
            return MessageType.Error;
        if (color.Color == Colors.Tomato)
            return MessageType.Warning;
        if (color.Color == Color.Parse("#A52A2A")) // Brown
            return MessageType.Path;
        // Add more conditions as needed
        return MessageType.Info;
    }

    private void OnExceptionOccurred(Exception ex)
    {
        ExceptionOccurred?.Invoke(ex);
    }

    public string GetPythonPath()
    {
        return _pythonEnvironmentManager.GetPythonLibraryPath();
    }
}