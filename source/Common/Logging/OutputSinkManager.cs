using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;

namespace Common.Logging;

public class OutputSinkManager : ILogEventSink
{
    private static List<Action<string>> Callbacks { get; } = new List<Action<string>>();

    internal OutputSinkManager() { }

    public static void AddLogCallback(Action<string> callback)
    {
        Callbacks.Add(callback);
    }

    public static bool RemoveLogCallback(Action<string> callback) => Callbacks.Remove(callback);

    public void Emit(LogEvent logEvent)
    {
        TextWriter textWriter = new StringWriter();
        logEvent.RenderMessage(textWriter);

        // Logger.Error(ex, ...) attaches the exception separately from the message template;
        // without this the callbacks (in-game console, headless server console) only ever see
        // the message and the actual failure stays invisible.
        if (logEvent.Exception != null)
        {
            textWriter.Write(Environment.NewLine);
            textWriter.Write(logEvent.Exception);
        }

        foreach (var callback in Callbacks)
        {
            callback(textWriter.ToString());
        }
    }
}
