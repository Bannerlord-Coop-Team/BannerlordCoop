using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;

namespace Missions.Battles;

/// <summary>
/// [Game thread] Hull-addressed naval messages that are sent once, or that depend on queued state, applied from the next naval
/// tick in arrival order: spawn records, removals, departures, entries, host changes, conditions, final ropes, routed damage and
/// capture traffic. The game-thread queue drains while the engine's async agent tick runs, and these change what it reads (R48 L6).
/// </summary>
public interface IBattleShipArrivals
{
    /// <summary>How many arrivals wait for the next tick (diagnostics).</summary>
    int Count { get; }

    /// <summary>Queues <paramref name="apply"/> behind everything that arrived before it; <paramref name="context"/> names it in a failure log.</summary>
    void Enqueue(Action apply, string context);

    /// <summary>[Mission tick] Applies every queued arrival in order, logging and skipping one that throws.</summary>
    void ApplyAll();

    /// <summary>Drops whatever still waits, as the battle ends.</summary>
    void Clear();
}

/// <inheritdoc cref="IBattleShipArrivals"/>
public class BattleShipArrivals : IBattleShipArrivals
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleShipArrivals>();

    private readonly Queue<(Action Apply, string Context)> arrivals = new Queue<(Action, string)>();

    // Read on the game thread with the queue it counts.
    public int Count => arrivals.Count;

    // Behind every arrival queued before it.
    public void Enqueue(Action apply, string context) => arrivals.Enqueue((apply, context));

    // An arrival queued while applying (a capture's own records) waits for the next tick, so one pass always ends.
    public void ApplyAll()
    {
        for (int pending = arrivals.Count; pending > 0; pending--)
        {
            var (apply, context) = arrivals.Dequeue();
            try
            {
                apply();
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "[NavalSync] Applying the arrived {Context} failed", context);
            }
        }
    }

    // Nothing queued applies after the battle ended.
    public void Clear() => arrivals.Clear();
}
