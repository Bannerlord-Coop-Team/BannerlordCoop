using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;

namespace Common.Logging;

public class OutputSinkManager : ILogEventSink
{
    private static List<Action<string>> Callbacks { get; } = new List<Action<string>>();
    private static List<Action<LogEvent>> EventCallbacks { get; } = new List<Action<LogEvent>>();

    internal OutputSinkManager() { }

    public static void AddLogCallback(Action<string> callback)
    {
        Callbacks.Add(callback);
    }

    public static bool RemoveLogCallback(Action<string> callback) => Callbacks.Remove(callback);

    public static void AddLogEventCallback(Action<LogEvent> callback)
    {
        EventCallbacks.Add(callback);
    }

    public static bool RemoveLogEventCallback(Action<LogEvent> callback) => EventCallbacks.Remove(callback);

    public void Emit(LogEvent logEvent)
    {
        TextWriter textWriter = new StringWriter();
        logEvent.RenderMessage(textWriter);

        foreach (var callback in Callbacks)
        {
            callback(textWriter.ToString());
        }

        foreach (var callback in EventCallbacks)
        {
            callback(logEvent);
        }
    }
}
