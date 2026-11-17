using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Common.Logging;

/// <summary>Hands each rendered log event to the registered callbacks whose minimum level it reaches.</summary>
public class OutputSinkManager : ILogEventSink
{
    private static List<(Action<string> Callback, LogEventLevel MinimumLevel)> Callbacks { get; } =
        new List<(Action<string>, LogEventLevel)>();

    // Emit runs on whatever thread logged (timers, pollers, thread-pool continuations) while
    // callbacks are added/removed on the main thread; an unguarded List enumeration racing a
    // mutation throws out of the sink into the logging caller.
    private static readonly object gate = new object();

    internal OutputSinkManager() { }

    /// <summary>Hands <paramref name="callback"/> every rendered log event.</summary>
    public static void AddLogCallback(Action<string> callback) => AddLogCallback(callback, LogEventLevel.Verbose);

    /// <summary>Hands <paramref name="callback"/> the rendered log events at <paramref name="minimumLevel"/> or above.</summary>
    public static void AddLogCallback(Action<string> callback, LogEventLevel minimumLevel)
    {
        lock (gate)
        {
            Callbacks.Add((callback, minimumLevel));
        }
    }

    /// <summary>Stops handing log events to <paramref name="callback"/>; false when it was not registered.</summary>
    public static bool RemoveLogCallback(Action<string> callback)
    {
        lock (gate)
        {
            int index = Callbacks.FindIndex(entry => entry.Callback == callback);
            if (index < 0) return false;

            Callbacks.RemoveAt(index);
            return true;
        }
    }

    /// <summary>Renders <paramref name="logEvent"/> with its exception and hands it to every callback its level reaches.</summary>
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
            callbacks = Callbacks.Where(entry => logEvent.Level >= entry.MinimumLevel).Select(entry => entry.Callback).ToArray();
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
