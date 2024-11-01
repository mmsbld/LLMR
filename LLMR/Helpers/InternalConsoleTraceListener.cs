using System;
using System.Diagnostics;

namespace LLMR.Helpers;

public class InternalConsoleTraceListener : TraceListener
{
    private readonly Action<string> _writeAction;

    public InternalConsoleTraceListener(Action<string> writeAction)
    {
        _writeAction = writeAction ?? throw new ArgumentNullException(nameof(writeAction));
    }

    public override void Write(string message)
    {
        _writeAction(message);
    }

    public override void WriteLine(string message)
    {
        _writeAction(message);
    }
}