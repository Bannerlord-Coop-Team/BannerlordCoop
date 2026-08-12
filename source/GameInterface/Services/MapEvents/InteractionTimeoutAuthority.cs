using GameInterface.Services.Players;
using GameInterface;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Owns the authoritative lifetime of map-party conversations. The Hex bridge
/// supplies the real-time clock tick and presentation; only this service may
/// release the exact Coop engagement and create the per-lord cooldown.
/// </summary>
public interface IInteractionTimeoutAuthority
{
    bool TryRegister(NetPeer peer, string targetPartyId, string requestId);
    bool IsBlocked(NetPeer peer, string targetPartyId, DateTime nowUtc, out TimeSpan remaining);
    InteractionExpiryResult TryExpire(
        NetPeer peer,
        string targetPartyId,
        string requestId,
        DateTime nowUtc,
        TimeSpan cooldown);
}

public enum InteractionExpiryResult
{
    Retry = 0,
    Stale = 1,
    Expired = 2
}

/// <summary>
/// Reflection-safe boundary used by optional client/server presentation modules.
/// Keeping the DI type behind this endpoint lets Coop remain the sole owner of
/// engagement state without creating a hard assembly-version dependency.
/// </summary>
public static class InteractionTimeoutAuthorityEndpoint
{
    public static int TryExpire(
        NetPeer peer,
        string targetPartyId,
        string requestId,
        DateTime nowUtc,
        TimeSpan cooldown)
    {
        if (!ContainerProvider.TryResolve<IInteractionTimeoutAuthority>(
                out var authority))
        {
            return (int)InteractionExpiryResult.Retry;
        }

        return (int)authority.TryExpire(
            peer,
            targetPartyId,
            requestId,
            nowUtc,
            cooldown);
    }
}

internal sealed class InteractionTimeoutAuthority : IInteractionTimeoutAuthority
{
    private readonly object stateLock = new object();
    private readonly Dictionary<CooldownKey, DateTime> cooldowns =
        new Dictionary<CooldownKey, DateTime>();
    // Coop permits one active conversation engagement per peer. A weak peer
    // key bounds this state to that engagement and lets disconnected peers be
    // collected instead of retaining every historical request GUID forever.
    private readonly ConditionalWeakTable<NetPeer, RegisteredTarget> stableTargets =
        new ConditionalWeakTable<NetPeer, RegisteredTarget>();
    private readonly ConversationPartyTracker tracker;
    private readonly IPlayerManager playerManager;

    public InteractionTimeoutAuthority(
        ConversationPartyTracker tracker,
        IPlayerManager playerManager)
    {
        this.tracker = tracker;
        this.playerManager = playerManager;
    }

    public bool TryRegister(
        NetPeer peer,
        string targetPartyId,
        string requestId)
    {
        targetPartyId ??= string.Empty;
        requestId ??= string.Empty;
        if (!TryGetControllerId(peer, out var controllerId) ||
            !TryResolveStableTarget(targetPartyId, out var stableTargetId))
        {
            return false;
        }

        lock (stateLock)
        {
            stableTargets.Remove(peer);
            stableTargets.Add(peer, new RegisteredTarget(
                controllerId,
                targetPartyId,
                requestId,
                stableTargetId));
        }
        return true;
    }

    public bool IsBlocked(
        NetPeer peer,
        string targetPartyId,
        DateTime nowUtc,
        out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (!TryCreateCooldownKey(peer, targetPartyId, out var key)) return false;

        lock (stateLock)
        {
            PruneExpiredNoLock(nowUtc);
            if (!cooldowns.TryGetValue(key, out var expiresUtc)) return false;

            remaining = expiresUtc - nowUtc;
            return remaining > TimeSpan.Zero;
        }
    }

    public InteractionExpiryResult TryExpire(
        NetPeer peer,
        string targetPartyId,
        string requestId,
        DateTime nowUtc,
        TimeSpan cooldown)
    {
        targetPartyId ??= string.Empty;
        requestId ??= string.Empty;
        if (!tracker.TryGetEngagement(peer, out var engagement) ||
            !string.Equals(engagement.PartyId, targetPartyId, StringComparison.Ordinal) ||
            !string.Equals(engagement.RequestId ?? string.Empty, requestId, StringComparison.Ordinal))
        {
            return InteractionExpiryResult.Stale;
        }

        if (!TryGetControllerId(peer, out var controllerId))
            return InteractionExpiryResult.Retry;

        string stableTargetId;

        lock (stateLock)
        {
            PruneExpiredNoLock(nowUtc);
            if (!stableTargets.TryGetValue(peer, out var registered) ||
                !registered.Matches(controllerId, targetPartyId, requestId))
                return InteractionExpiryResult.Retry;
            stableTargetId = registered.StableTargetId;
        }

        ConversationPartyHold.EndEngagement(
            tracker,
            peer,
            requestId,
            requireRequestIdMatch: true);
        if (tracker.TryGetEngagement(peer, out var remaining) &&
            string.Equals(remaining.PartyId, targetPartyId, StringComparison.Ordinal) &&
            string.Equals(remaining.RequestId ?? string.Empty, requestId, StringComparison.Ordinal))
        {
            return InteractionExpiryResult.Retry;
        }

        lock (stateLock)
        {
            stableTargets.Remove(peer);
            cooldowns[new CooldownKey(controllerId, stableTargetId)] =
                nowUtc.Add(cooldown);
        }
        return InteractionExpiryResult.Expired;
    }

    private bool TryCreateCooldownKey(
        NetPeer peer,
        string targetPartyId,
        out CooldownKey key)
    {
        key = default;
        if (!TryGetControllerId(peer, out var controllerId) ||
            !TryResolveStableTarget(targetPartyId, out var stableTargetId))
        {
            return false;
        }

        key = new CooldownKey(controllerId, stableTargetId);
        return true;
    }

    private bool TryGetControllerId(NetPeer peer, out string controllerId)
    {
        controllerId = null;
        if (peer == null ||
            !playerManager.TryGetPlayer(peer, out var player) ||
            string.IsNullOrEmpty(player.ControllerId))
        {
            return false;
        }
        controllerId = player.ControllerId;
        return true;
    }

    private bool TryResolveStableTarget(
        string targetPartyId,
        out string stableTargetId)
    {
        stableTargetId = null;
        if (string.IsNullOrEmpty(targetPartyId) ||
            !tracker.ObjectManager.TryGetObject(
                targetPartyId,
                out TaleWorlds.CampaignSystem.Party.PartyBase party))
        {
            return false;
        }

        if (party.LeaderHero != null)
        {
            return tracker.ObjectManager.TryGetId(
                       party.LeaderHero, out stableTargetId) &&
                   !string.IsNullOrEmpty(stableTargetId);
        }

        stableTargetId = targetPartyId;
        return true;
    }

    private void PruneExpiredNoLock(DateTime nowUtc)
    {
        if (cooldowns.Count == 0) return;

        var expired = new List<CooldownKey>();
        foreach (var pair in cooldowns)
        {
            if (pair.Value <= nowUtc)
                expired.Add(pair.Key);
        }

        foreach (var key in expired)
            cooldowns.Remove(key);
    }

    private readonly struct CooldownKey : IEquatable<CooldownKey>
    {
        private readonly string controllerId;
        private readonly string targetId;

        internal CooldownKey(string controllerId, string targetId)
        {
            this.controllerId = controllerId;
            this.targetId = targetId;
        }

        public bool Equals(CooldownKey other) =>
            string.Equals(controllerId, other.controllerId, StringComparison.Ordinal) &&
            string.Equals(targetId, other.targetId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is CooldownKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((controllerId != null ? controllerId.GetHashCode() : 0) * 397) ^
                    (targetId != null ? targetId.GetHashCode() : 0);
            }
        }
    }

    private sealed class RegisteredTarget
    {
        private readonly string controllerId;
        private readonly string targetPartyId;
        private readonly string requestId;

        internal RegisteredTarget(
            string controllerId,
            string targetPartyId,
            string requestId,
            string stableTargetId)
        {
            this.controllerId = controllerId;
            this.targetPartyId = targetPartyId;
            this.requestId = requestId;
            StableTargetId = stableTargetId;
        }

        internal string StableTargetId { get; }

        internal bool Matches(
            string currentControllerId,
            string currentTargetPartyId,
            string currentRequestId) =>
            string.Equals(controllerId, currentControllerId, StringComparison.Ordinal) &&
            string.Equals(targetPartyId, currentTargetPartyId, StringComparison.Ordinal) &&
            string.Equals(requestId, currentRequestId, StringComparison.Ordinal);
    }
}
