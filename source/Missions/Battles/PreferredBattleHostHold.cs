using Common.Logging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Missions.Battles;

/// <summary>What a held election wants the caller to do next.</summary>
internal enum HeldElectionDecision
{
    /// <summary>No election is held for the battle, so the request is processed now.</summary>
    NotHeld,
    /// <summary>The election is held, so nobody is elected yet.</summary>
    Held,
    /// <summary>Nobody is awaited any more, so the held requests are released and elected.</summary>
    Release
}

/// <summary>[Server, game thread] Holds a battle's first host election while a higher ranked preferred host is loading.</summary>
internal interface IPreferredBattleHostHold
{
    /// <summary>How long the first election waits for a higher ranked preferred host that is still loading.</summary>
    TimeSpan Wait { get; set; }

    /// <summary>Whether any battle has a held election.</summary>
    bool HasHeldElections { get; }

    /// <summary>Holds a mission-ready host request, or updates a held one in place. <paramref name="isLoading"/> says
    /// whether a player is in the battle, connected and not departed, withdrawn or absent.</summary>
    HeldElectionDecision Request(string mapEventId, string controllerId, NetPeer peer, bool hideoutStateReady,
        Func<Player, bool> isLoading);

    /// <summary>Forgets a player that left or disconnected, so a gone player is never elected.</summary>
    HeldElectionDecision Drop(string mapEventId, string controllerId, Func<Player, bool> isLoading);

    /// <summary>The battles whose wait has run out.</summary>
    List<string> ExpiredBattles(DateTime now);

    /// <summary>Ends the battle's hold and returns its requests in mission-ready order, or null when none is held.</summary>
    IReadOnlyList<HeldHostRequest> Release(string mapEventId);

    /// <summary>Drops the battle's hold without electing anyone.</summary>
    void Clear(string mapEventId);
}

/// <summary>
/// Holds the first host election of a battle while a listed player (<see cref="IPreferredBattleHosts"/>) ranked
/// above every ready requester is still loading, so a slower preferred player can host instead of whoever loaded
/// first. Requests queue in mission-ready order until nobody is awaited or <see cref="Wait"/> runs out, then the
/// caller releases them and elects. The election itself stays in <see cref="BattleHostHandler"/>.
/// </summary>
internal class PreferredBattleHostHold : IPreferredBattleHostHold
{
    private static readonly ILogger Logger = LogManager.GetLogger<PreferredBattleHostHold>();

    private readonly IPlayerManager playerManager;
    private readonly IBattleHostRegistry hostRegistry;
    private readonly IPreferredBattleHosts preferredHosts;

    // Elections held for a loading preferred host, keyed by battle. Game thread only.
    private readonly Dictionary<string, PendingElection> pendingElections = new Dictionary<string, PendingElection>();

    private sealed class PendingElection
    {
        public DateTime DeadlineUtc;
        public readonly List<HeldHostRequest> Requests = new List<HeldHostRequest>();
    }

    public PreferredBattleHostHold(IPlayerManager playerManager, IBattleHostRegistry hostRegistry,
        IPreferredBattleHosts preferredHosts)
    {
        this.playerManager = playerManager;
        this.hostRegistry = hostRegistry;
        this.preferredHosts = preferredHosts;
    }

    public TimeSpan Wait { get; set; } = TimeSpan.FromSeconds(5);

    public bool HasHeldElections => pendingElections.Count > 0;

    public HeldElectionDecision Request(string mapEventId, string controllerId, NetPeer peer, bool hideoutStateReady,
        Func<Player, bool> isLoading)
    {
        if (pendingElections.TryGetValue(mapEventId, out var pending))
        {
            // A duplicate updates in place so the requester keeps its mission-ready place for the tie break.
            var held = pending.Requests.Find(request => request.ControllerId == controllerId);
            if (held == null)
                pending.Requests.Add(held = new HeldHostRequest { ControllerId = controllerId });
            held.Peer = peer;
            held.HideoutStateReady = hideoutStateReady;
            return ReleaseIfNobodyAwaited(mapEventId, pending, leftControllerId: null, isLoading);
        }

        // With no list everyone is unlisted, so nobody outranks the requester and the first ready player is elected.
        if (hostRegistry.TryGet(mapEventId, out _) ||
            !IsHigherRankedHostLoading(preferredHosts.RankOf(controllerId), null, controllerId, isLoading))
            return HeldElectionDecision.NotHeld;

        pending = new PendingElection { DeadlineUtc = DateTime.UtcNow + Wait };
        pending.Requests.Add(new HeldHostRequest { ControllerId = controllerId, Peer = peer, HideoutStateReady = hideoutStateReady });
        pendingElections[mapEventId] = pending;
        Logger.Information("[BattleHost] Holding the election for {MapEventId} up to {Wait} for a higher ranked preferred host still loading",
            mapEventId, Wait);
        return HeldElectionDecision.Held;
    }

    public HeldElectionDecision Drop(string mapEventId, string controllerId, Func<Player, bool> isLoading)
    {
        if (!pendingElections.TryGetValue(mapEventId, out var pending)) return HeldElectionDecision.NotHeld;

        pending.Requests.RemoveAll(request => request.ControllerId == controllerId);
        return ReleaseIfNobodyAwaited(mapEventId, pending, controllerId, isLoading);
    }

    public List<string> ExpiredBattles(DateTime now) =>
        pendingElections.Where(pair => now >= pair.Value.DeadlineUtc).Select(pair => pair.Key).ToList();

    public IReadOnlyList<HeldHostRequest> Release(string mapEventId)
    {
        if (!pendingElections.TryGetValue(mapEventId, out var pending)) return null;
        pendingElections.Remove(mapEventId);
        return pending.Requests;
    }

    public void Clear(string mapEventId) => pendingElections.Remove(mapEventId);

    // Ends the hold early once no listed player ranked above the best held requester is still loading. A hold
    // left with no requesters is dropped, so the next ready player starts a fresh one.
    private HeldElectionDecision ReleaseIfNobodyAwaited(string mapEventId, PendingElection pending,
        string leftControllerId, Func<Player, bool> isLoading)
    {
        if (pending.Requests.Count == 0)
        {
            pendingElections.Remove(mapEventId);
            Logger.Information("[BattleHost] Dropped the held election for {MapEventId}: every held requester left", mapEventId);
            return HeldElectionDecision.NotHeld;
        }

        int bestReadyRank = pending.Requests.Min(request => preferredHosts.RankOf(request.ControllerId));
        if (IsHigherRankedHostLoading(bestReadyRank, pending, leftControllerId, isLoading))
            return HeldElectionDecision.Held;
        return HeldElectionDecision.Release;
    }

    // True while a listed player ranked above `rank` is still loading and has not sent its own host request yet.
    // The excluded one (the requester just ready or the player just leaving) is not waited for.
    private bool IsHigherRankedHostLoading(int rank, PendingElection pending, string excludedControllerId,
        Func<Player, bool> isLoading)
    {
        foreach (var player in playerManager.Players)
        {
            var controllerId = player.ControllerId;
            if (preferredHosts.RankOf(controllerId) >= rank || controllerId == excludedControllerId)
                continue;
            if (pending != null && pending.Requests.Exists(request => request.ControllerId == controllerId))
                continue;
            if (isLoading(player))
                return true;
        }
        return false;
    }
}

/// <summary>A mission-ready host request queued while the election is held.</summary>
internal sealed class HeldHostRequest
{
    public string ControllerId;
    public NetPeer Peer;
    public bool HideoutStateReady;
}
