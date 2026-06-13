using Common.Util;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Common.Logging;

/// <summary>
/// Tallies outbound network <see cref="Common.Messaging.IMessage"/> traffic and periodically dumps
/// the per-type counts to the log, to profile which messages dominate network activity.
/// </summary>
/// <remarks>
/// Fed from the network send path (see <c>CoopNetworkBase</c>), so every recorded message is one
/// actually sent over the wire. The accumulated counts are dumped on a fixed wall-clock interval.
/// Only the server profiles traffic (see <see cref="ModInformation.IsServer"/>).
/// </remarks>
public sealed class MessageProfiler : IDisposable
{
    // A logger to dump the message profile.
    private static readonly ILogger Logger = LogManager.GetLogger<MessageProfiler>();

    // A task to periodically dump the accumulated counts.
    private readonly Poller poller;

    private readonly ConcurrentDictionary<string, int> counts = new ConcurrentDictionary<string, int>();

    /// <summary>
    /// Constructs a MessageProfiler.
    /// </summary>
    /// <param name="dumpInterval">How often to dump the accumulated counts to the log.</param>
    public MessageProfiler(TimeSpan dumpInterval)
    {
        poller = new Poller(Poll, dumpInterval);
        poller.Start();
    }

    /// <summary>
    /// Records one message sent over the network. No-op off the server.
    /// </summary>
    public void Record(Type messageType)
    {
        // Only the server profiles message traffic.
        if (ModInformation.IsClient) return;

        var messageName = GetFriendlyTypeName(messageType);

        counts.AddOrUpdate(messageName, 1, (_, value) => value + 1);
    }

    // Dumps the accumulated per-type counts and clears them for the next window.
    private void Poll(TimeSpan dt)
    {
        if (counts.IsEmpty) return;

        // Drain the counts into a snapshot so the profile is logged as a single structured
        // property and the dictionary is cleared for the next window.
        var snapshot = new Dictionary<string, int>(counts.Count);
        foreach (var messageName in counts.Keys)
        {
            if (counts.TryRemove(messageName, out var count) && count > 0)
            {
                snapshot[messageName] = count;
            }
        }

        // Order most-sent first and format each entry as a friendly "name: count" string. A list
        // is rendered in order by Serilog (a Dictionary's key order is not preserved by the sinks,
        // and raw key/value pairs get destructured into noisy $type objects).
        var ordered = snapshot
            .OrderByDescending(entry => entry.Value)
            .Select(entry => $"{entry.Key}: {entry.Value}")
            .ToList();

        Logger.Information("Message profile over {Seconds:0.#} seconds: {@MessageCounts}", dt.TotalSeconds, ordered);
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name;
        var tickIndex = name.IndexOf('`');
        if (tickIndex > 0)
            name = name.Substring(0, tickIndex);

        var genericArgs = type.GetGenericArguments();
        var argNames = new string[genericArgs.Length];

        for (int i = 0; i < genericArgs.Length; i++)
        {
            argNames[i] = GetFriendlyTypeName(genericArgs[i]);
        }

        return $"{name}<{string.Join(", ", argNames)}>";
    }

    /// <summary>
    /// Disposes of the MessageProfiler, stopping the periodic dump.
    /// </summary>
    public void Dispose()
    {
        poller.Stop();
    }
}
