using System;
using System.Collections.Generic;
using System.Linq;
using Missions.Agents.Packets;

namespace Missions.Agents.Handlers;

/// <summary>Game-thread deck send measurements and DEBUG receiver fault injection, owned by one movement handler.</summary>
public interface INavalDeckMovementProbe
{
    /// <summary>Records one successfully sent AI deck pose, separately for each recipient.</summary>
    void Record(string recipient, Guid agent, AgentData data, bool ai, float time);
    /// <summary>Discards a departed recipient's measurements.</summary>
    void RemoveRecipient(string recipient);
    /// <summary>Starts fresh when the mission's deck seam changes.</summary>
    void Reset();
    /// <summary>Reports the last ten simulation seconds of actual per-agent sends.</summary>
    object Inspect(float time);
#if DEBUG
    /// <summary>Replaces the selected remote agent's rejection budget; zero cancels it.</summary>
    void RejectNext(Guid agent, int count);
    /// <summary>Consumes one forced rejection only for the selected received deck pose.</summary>
    bool ShouldReject(Guid agent);
#endif
}

/// <inheritdoc cref="INavalDeckMovementProbe"/>
public sealed class NavalDeckMovementProbe : INavalDeckMovementProbe
{
    private const float WindowSeconds = 10f;
    private const float IdleSpeed = 0.1f;
    private readonly Dictionary<(string Recipient, Guid Agent), Queue<(float Time, Guid Ship, float Speed)>> sends = new();
#if DEBUG
    private Guid rejectAgent;
    private int remaining;
    private long injected;
#endif

    // Retains successful sends only, so budget deferral and recipient cadence remain visible.
    public void Record(string recipient, Guid agent, AgentData data, bool ai, float time)
    {
        var key = (recipient, agent);
        if (!ai || data.DeckShip == Guid.Empty)
        {
            sends.Remove(key);
            return;
        }
        if (!sends.TryGetValue(key, out var samples))
        {
            samples = new Queue<(float, Guid, float)>();
            sends.Add(key, samples);
        }
        samples.Enqueue((time, data.DeckShip, data.Speed));
        Trim(samples, time);
    }

    // Prevents a rejoined recipient from inheriting its old samples.
    public void RemoveRecipient(string recipient)
    {
        foreach (var key in sends.Keys.Where(key => key.Recipient == recipient).ToArray()) sends.Remove(key);
    }

    // Deck configuration and teardown share the same diagnostic lifetime.
    public void Reset()
    {
        sends.Clear();
#if DEBUG
        rejectAgent = Guid.Empty;
        remaining = 0;
        injected = 0;
#endif
    }

    // Both ends of an interval must be inside the window and on the same hull.
    public object Inspect(float time)
    {
        foreach (var key in sends.Keys.ToArray())
        {
            Trim(sends[key], time);
            if (sends[key].Count == 0) sends.Remove(key);
        }
        return new
        {
            windowSeconds = WindowSeconds,
            idleSpeedMetresPerSecond = IdleSpeed,
            measurement = "Successful AI deck pose sends per recipient, in simulation seconds. Idle intervals require both endpoint speeds <= 0.1 m/s; intervening motion is not sampled. Seated crews send no poses.",
            agents = sends.OrderBy(pair => pair.Key.Recipient).ThenBy(pair => pair.Key.Agent).Select(pair =>
            {
                var samples = pair.Value.ToArray();
                var intervals = Enumerable.Range(1, samples.Length - 1)
                    .Where(i => samples[i].Ship == samples[i - 1].Ship).ToArray();
                var idle = intervals.Where(i => samples[i].Speed <= IdleSpeed && samples[i - 1].Speed <= IdleSpeed).ToArray();
                return new
                {
                    recipient = pair.Key.Recipient, agentId = pair.Key.Agent,
                    count = samples.Length, firstSentAt = samples[0].Time, lastSentAt = samples[samples.Length - 1].Time,
                    intervalCount = intervals.Length,
                    meanIntervalSeconds = intervals.Length == 0 ? (float?)null : intervals.Average(i => samples[i].Time - samples[i - 1].Time),
                    maxIntervalSeconds = intervals.Length == 0 ? (float?)null : intervals.Max(i => samples[i].Time - samples[i - 1].Time),
                    idleIntervalCount = idle.Length,
                    idleMeanIntervalSeconds = idle.Length == 0 ? (float?)null : idle.Average(i => samples[i].Time - samples[i - 1].Time),
                    idleMaxIntervalSeconds = idle.Length == 0 ? (float?)null : idle.Max(i => samples[i].Time - samples[i - 1].Time),
                    lastSpeed = samples[samples.Length - 1].Speed, maxSpeed = samples.Max(sample => sample.Speed),
                };
            }).ToArray(),
#if DEBUG
            rejection = new { agentId = rejectAgent, remaining, injected },
#endif
        };
    }

    // Drops old timestamps before computing intervals, including after a sender stops entirely.
    private void Trim(Queue<(float Time, Guid Ship, float Speed)> samples, float time)
    {
        while (samples.Count > 0 && time - samples.Peek().Time > WindowSeconds) samples.Dequeue();
    }

#if DEBUG
    // Only the receiving client changes its own puppet driver; no authoritative state is changed.
    public void RejectNext(Guid agent, int count)
    {
        rejectAgent = agent;
        remaining = count;
    }

    // Called after normal sender, authority and sequence checks, immediately before deck validation.
    public bool ShouldReject(Guid agent)
    {
        if (agent != rejectAgent || remaining == 0) return false;
        remaining--;
        injected++;
        return true;
    }
#endif
}
