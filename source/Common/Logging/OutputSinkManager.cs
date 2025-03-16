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

        foreach (var callback in Callbacks)
        {
            callback(textWriter.ToString());
        }
    }
}
