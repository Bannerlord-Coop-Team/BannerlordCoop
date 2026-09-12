using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;

namespace Common.Logging;

public class OutputSinkManager : ILogEventSink
{
    private static List<Action<string>> Callbacks { get; } = new List<Action<string>>();

    // Emit runs on whatever thread logged (timers, pollers, thread-pool continuations) while
    // callbacks are added/removed on the main thread; an unguarded List enumeration racing a
    // mutation throws out of the sink into the logging caller.
    private static readonly object gate = new object();

    internal OutputSinkManager() { }

    public static void AddLogCallback(Action<string> callback)
    {
        lock (gate)
        {
            Callbacks.Add(callback);
        }
    }

    public static bool RemoveLogCallback(Action<string> callback)
    {
        lock (gate)
        {
            return Callbacks.Remove(callback);
        }
    }

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

        Action<string>[] callbacks;
        lock (gate)
        {
            callbacks = Callbacks.ToArray();
        }

        foreach (var callback in callbacks)
        {
            try
            {
                callback(textWriter.ToString());
            }
            catch
            {
                // A dead sink (e.g. an xUnit output helper whose test already finished when a
                // background thread logs) must not take down the logging caller or starve the
                // remaining sinks.
            }
        }
    }
}
