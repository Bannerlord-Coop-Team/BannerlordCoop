using Common.Messaging;
using Missions.Agents.Messages;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

public interface IGuardedHitWindow : IDisposable
{
    long Epoch { get; }
    void Advance();
    bool TryConsumeGuarded(Agent affectedAgent, Agent affectorAgent);
    void Reset();
}

public sealed class GuardedHitWindow : IGuardedHitWindow
{
    private const int RetainedEpochs = 2;

    private readonly IMessageBroker messageBroker;
    private readonly Dictionary<HitPair, Queue<long>> guardedUntilEpoch = new();
    private bool disposed;

    public long Epoch { get; private set; }

    public GuardedHitWindow(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<LocalAgentGuardedHit>(
            Handle_LocalAgentGuardedHit);
    }

    public void Advance()
    {
        if (disposed)
            return;

        Epoch++;
        if (guardedUntilEpoch.Count == 0)
            return;

        var expired = new List<HitPair>();
        foreach (var guardedHit in guardedUntilEpoch)
        {
            RemoveExpired(guardedHit.Value);
            if (guardedHit.Value.Count == 0)
                expired.Add(guardedHit.Key);
        }

        foreach (HitPair pair in expired)
            guardedUntilEpoch.Remove(pair);
    }

    public bool TryConsumeGuarded(
        Agent affectedAgent,
        Agent affectorAgent)
    {
        if (disposed ||
            affectedAgent == null ||
            affectorAgent == null)
        {
            return false;
        }

        var pair = new HitPair(affectedAgent, affectorAgent);
        if (!guardedUntilEpoch.TryGetValue(
                pair,
                out Queue<long> retainedUntil))
        {
            return false;
        }

        RemoveExpired(retainedUntil);
        if (retainedUntil.Count == 0)
        {
            guardedUntilEpoch.Remove(pair);
            return false;
        }

        retainedUntil.Dequeue();
        if (retainedUntil.Count == 0)
            guardedUntilEpoch.Remove(pair);
        return true;
    }

    public void Reset()
    {
        guardedUntilEpoch.Clear();
        Epoch = 0;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        messageBroker.Unsubscribe<LocalAgentGuardedHit>(
            Handle_LocalAgentGuardedHit);
        guardedUntilEpoch.Clear();
    }

    private void Handle_LocalAgentGuardedHit(
        MessagePayload<LocalAgentGuardedHit> payload)
    {
        LocalAgentGuardedHit guardedHit = payload.What;
        if (disposed ||
            guardedHit?.AffectedAgent == null ||
            guardedHit.AffectorAgent == null)
        {
            return;
        }

        var pair = new HitPair(
            guardedHit.AffectedAgent,
            guardedHit.AffectorAgent);
        if (!guardedUntilEpoch.TryGetValue(
                pair,
                out Queue<long> retainedUntil))
        {
            retainedUntil = new Queue<long>();
            guardedUntilEpoch.Add(pair, retainedUntil);
        }

        retainedUntil.Enqueue(Epoch + RetainedEpochs);
    }

    private void RemoveExpired(Queue<long> retainedUntil)
    {
        while (retainedUntil.Count > 0 &&
               retainedUntil.Peek() < Epoch)
        {
            retainedUntil.Dequeue();
        }
    }

    private readonly struct HitPair : IEquatable<HitPair>
    {
        private readonly Agent affectedAgent;
        private readonly Agent affectorAgent;

        public HitPair(
            Agent affectedAgent,
            Agent affectorAgent)
        {
            this.affectedAgent = affectedAgent;
            this.affectorAgent = affectorAgent;
        }

        public bool Equals(HitPair other)
        {
            return ReferenceEquals(
                    affectedAgent,
                    other.affectedAgent)
                && ReferenceEquals(
                    affectorAgent,
                    other.affectorAgent);
        }

        public override bool Equals(object obj)
        {
            return obj is HitPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (RuntimeHelpers.GetHashCode(affectedAgent) * 397)
                ^ RuntimeHelpers.GetHashCode(affectorAgent);
        }
    }
}
