using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using LiteNetLib;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace Coop.Core.Server.Services.Instances.Handlers;

/// <summary>Concludes a shared battle once every current mission member reports the host's result.</summary>
public class ServerBattleCompletionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerBattleCompletionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IMissionManager missionManager;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IBattleHostRegistry hostRegistry;
    private readonly IBattleCompletionTracker completionTracker;
    private readonly IBattleTroopReserveBuilder reserveBuilder;
    private readonly object pendingJoinersGate = new object();
    private readonly Dictionary<string, Dictionary<Guid, (string ControllerId, NetPeer Peer, DateTime ExpiresUtc)>> pendingJoiners = new();
    private readonly Dictionary<string, DateTime> conclusionRetries = new();
    // When each kept host's battle stops waiting for members that never match its report (Q7).
    private readonly Dictionary<string, DateTime> keptHostDeadlines = new();

    internal TimeSpan JoinReservationTimeout { get; set; } = TimeSpan.FromMinutes(2);
    internal TimeSpan ConclusionRetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>How long a kept host's battle waits for its remaining members before its report concludes it alone.</summary>
    internal TimeSpan KeptHostReportTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public ServerBattleCompletionHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IMissionManager missionManager,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IBattleHostRegistry hostRegistry,
        IBattleCompletionTracker completionTracker,
        IBattleTroopReserveBuilder reserveBuilder)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.missionManager = missionManager;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.hostRegistry = hostRegistry;
        this.completionTracker = completionTracker;
        this.reserveBuilder = reserveBuilder;

        messageBroker.Subscribe<NetworkBattleResultReady>(Handle_NetworkBattleResultReady);
        messageBroker.Subscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Subscribe<BattleHostAssignmentChanged>(Handle_BattleHostAssignmentChanged);
        messageBroker.Subscribe<BattleJoinAccepted>(Handle_BattleJoinAccepted);
        messageBroker.Subscribe<BattleJoinCancelled>(Handle_BattleJoinCancelled);
        messageBroker.Subscribe<NetworkFieldBattleOpenRefused>(Handle_NetworkFieldBattleOpenRefused);
        messageBroker.Subscribe<MissionMemberEntered>(Handle_MissionMemberEntered);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
        messageBroker.Subscribe<BattleStateChangeProcessed>(Handle_BattleStateChangeProcessed);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleResultReady>(Handle_NetworkBattleResultReady);
        messageBroker.Unsubscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Unsubscribe<BattleHostAssignmentChanged>(Handle_BattleHostAssignmentChanged);
        messageBroker.Unsubscribe<BattleJoinAccepted>(Handle_BattleJoinAccepted);
        messageBroker.Unsubscribe<BattleJoinCancelled>(Handle_BattleJoinCancelled);
        messageBroker.Unsubscribe<NetworkFieldBattleOpenRefused>(Handle_NetworkFieldBattleOpenRefused);
        messageBroker.Unsubscribe<MissionMemberEntered>(Handle_MissionMemberEntered);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
        messageBroker.Unsubscribe<BattleStateChangeProcessed>(Handle_BattleStateChangeProcessed);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
    }

    private void Handle_NetworkBattleResultReady(MessagePayload<NetworkBattleResultReady> payload)
    {
        if (payload.Who is not NetPeer peer || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Warning("Ignoring battle result report for {Instance}: sender has no registered player",
                payload.What.InstanceId);
            return;
        }

        var controllerId = player.ControllerId;
        var result = payload.What;
        RunSerialized(() => TryRecordBattleResult(controllerId, result));
    }

    private void TryRecordBattleResult(string controllerId, NetworkBattleResultReady result)
    {
        lock (pendingJoinersGate)
        {
            var instanceId = result.InstanceId;
            if (!missionManager.TryGetControllers(instanceId, out var currentMembers))
            {
                Logger.Information("Ignoring battle result report from {Controller} for inactive instance {Instance}",
                    controllerId, instanceId);
                return;
            }

            if (!hostRegistry.TryGet(instanceId, out var assignment))
                return;

            if (!IsSupportedBattleResult(instanceId, result.BattleState))
                return;

            bool canConclude = !HasPendingJoiners(instanceId);
            bool isCurrentHostResult =
                currentMembers.Contains(controllerId) &&
                controllerId == assignment.HostControllerId &&
                result.HostEpoch == assignment.Epoch;

            bool concluded = completionTracker.TryRecordResult(
                    instanceId,
                    controllerId,
                    result.BattleState,
                    result.HostEpoch,
                    currentMembers,
                    assignment.HostControllerId,
                    assignment.Epoch,
                    out var concludedState,
                    canConclude,
                    hostKept: assignment.KeptAfterResult);

            if (isCurrentHostResult)
                messageBroker.Publish(this, new BattleResolvedStateRecorded(instanceId, result.BattleState));

            if (!concluded)
                return;

            if (!missionManager.TryBeginActiveInstanceConclusion(instanceId, currentMembers))
                return;

            PublishConclusion(instanceId, currentMembers.Count, concludedState, assignment.Epoch);
        }
    }

    private bool IsSupportedBattleResult(string instanceId, BattleState battleState)
    {
        if (battleState == BattleState.AttackerVictory || battleState == BattleState.DefenderVictory)
            return true;

        return battleState == BattleState.DefenderPullBack
            && objectManager.TryGetObject<MapEvent>(instanceId, out var mapEvent)
            && mapEvent.IsSiegeAmbush
            && mapEvent.BattleState == BattleState.None;
    }

    private void Handle_MissionMemberDeparted(MessagePayload<MissionMemberDeparted> payload)
    {
        var departure = payload.What;
        RunSerialized(() =>
        {
            RemovePendingJoiner(departure.InstanceId, departure.ControllerId, Guid.Empty);
            completionTracker.MemberDeparted(departure.InstanceId, departure.ControllerId);
            TryConcludeReportedBattle(departure.InstanceId);
        });
    }

    private void Handle_BattleHostAssignmentChanged(MessagePayload<BattleHostAssignmentChanged> payload)
    {
        var mapEventId = payload.What.MapEventId;
        RunSerialized(() =>
        {
            if (hostRegistry.TryGet(mapEventId, out var assignment))
            {
                completionTracker.HostAssigned(
                    mapEventId,
                    assignment.HostControllerId,
                    assignment.Epoch);
                if (assignment.KeptAfterResult && !keptHostDeadlines.ContainsKey(mapEventId))
                    keptHostDeadlines[mapEventId] = DateTime.UtcNow + KeptHostReportTimeout;
            }

            TryConcludeReportedBattle(mapEventId);
        });
    }

    // Concludes the battle once its reports agree, holding it while an accepted join has not entered yet.
    private void TryConcludeReportedBattle(string instanceId)
    {
        lock (pendingJoinersGate)
        {
            bool hasPendingJoiners = HasPendingJoiners(instanceId);
            if (!missionManager.TryGetControllers(instanceId, out var currentMembers))
            {
                if (!hasPendingJoiners)
                    TryConcludeAbandonedBattle(instanceId);

                return;
            }

            if (!hostRegistry.TryGet(instanceId, out var assignment))
                return;

            if (!completionTracker.TryReconcile(
                    instanceId,
                    currentMembers,
                    assignment.HostControllerId,
                    assignment.Epoch,
                    out var concludedState,
                    !hasPendingJoiners,
                    hostKept: assignment.KeptAfterResult))
            {
                return;
            }

            if (!missionManager.TryBeginActiveInstanceConclusion(instanceId, currentMembers))
                return;

            PublishConclusion(instanceId, currentMembers.Count, concludedState, assignment.Epoch);
        }
    }

    // Concludes an emptied battle from its retained reports. Callers hold it while a join is pending, and the
    // cancel, refusal and expiry paths call back in once the join is released.
    private void TryConcludeAbandonedBattle(string instanceId)
    {
        if (!completionTracker.TryConcludeAbandoned(
                instanceId,
                out var abandonedState,
                out var abandonedEpoch,
                out var abandonedMemberCount))
        {
            return;
        }

        ConcludeAbandonedBattle(instanceId, abandonedMemberCount, abandonedState, abandonedEpoch);
    }

    private bool HasPendingJoiners(string instanceId)
    {
        lock (pendingJoinersGate)
            return pendingJoiners.TryGetValue(instanceId, out var joiners) && joiners.Count > 0;
    }

    private void Handle_BattleJoinAccepted(MessagePayload<BattleJoinAccepted> payload)
    {
        if (payload.Who is not NetPeer peer)
            return;

        var reservation = payload.What;
        RunSerialized(() =>
        {
            lock (pendingJoinersGate)
            {
                if (missionManager.TryGetControllers(reservation.InstanceId, out var members) &&
                    members.Contains(reservation.ControllerId))
                {
                    return;
                }

                if (!pendingJoiners.TryGetValue(reservation.InstanceId, out var joiners))
                {
                    joiners = new Dictionary<Guid, (string, NetPeer, DateTime)>();
                    pendingJoiners[reservation.InstanceId] = joiners;
                }

                joiners[reservation.ReservationId] = (
                    reservation.ControllerId,
                    peer,
                    DateTime.UtcNow + JoinReservationTimeout);
            }
        });
    }

    private void Handle_BattleJoinCancelled(MessagePayload<BattleJoinCancelled> payload)
    {
        var cancellation = payload.What;
        RunSerialized(() =>
        {
            RemovePendingJoiner(
                cancellation.InstanceId,
                cancellation.ControllerId,
                cancellation.ReservationId);
            TryConcludeReportedBattle(cancellation.InstanceId);
        });
    }

    // A client that refused to open the field battle will not enter it, so its join no longer holds the conclusion.
    private void Handle_NetworkFieldBattleOpenRefused(MessagePayload<NetworkFieldBattleOpenRefused> payload)
    {
        var mapEventId = payload.What.MapEventId;
        if (payload.Who is not NetPeer peer || !playerManager.TryGetCurrentController(peer, out var controllerId))
        {
            Logger.Warning("Ignoring refused field battle open for {Instance}: sender is not a current player", mapEventId);
            return;
        }

        RunSerialized(() =>
        {
            int released = RemovePeerReservations(mapEventId, controllerId, peer);
            if (released == 0)
            {
                // The reservation already expired or the player entered, so there is nothing left to release.
                Logger.Information("Refused field battle open from {Controller} for {Instance} had no pending join to release",
                    controllerId, mapEventId);
                return;
            }

            Logger.Information("Released {Count} pending join(s) for {Controller} in {Instance}: the client refused to open the field battle",
                released, controllerId, mapEventId);
            TryConcludeReportedBattle(mapEventId);
        });
    }

    private void Handle_MissionMemberEntered(MessagePayload<MissionMemberEntered> payload)
    {
        var entry = payload.What;
        RunSerialized(() =>
        {
            completionTracker.ResetMember(
                entry.InstanceId,
                entry.ControllerId,
                entry.IsFirstMember);
            // A new instance of the map event must not inherit the kept-host deadline of the one that emptied.
            if (entry.IsFirstMember)
                keptHostDeadlines.Remove(entry.InstanceId);
            RemovePendingJoiner(entry.InstanceId, entry.ControllerId, Guid.Empty);
            // Keep campaign objects alive until the entrant's loaded mission reports its own result.
        });
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        var peer = payload.What.PlayerId;
        RunSerialized(() =>
        {
            List<string> changedInstances = new List<string>();
            lock (pendingJoinersGate)
            {
                foreach (var entry in pendingJoiners)
                {
                    var disconnected = entry.Value
                        .Where(joiner => ReferenceEquals(joiner.Value.Peer, peer))
                        .Select(joiner => joiner.Key)
                        .ToList();
                    foreach (var reservationId in disconnected)
                        entry.Value.Remove(reservationId);
                    if (disconnected.Count > 0)
                        changedInstances.Add(entry.Key);
                }

                RemoveEmptyPendingInstances();
            }

            foreach (var instanceId in changedInstances)
                TryConcludeReportedBattle(instanceId);
        });
    }

    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload)
    {
        var mapEvent = payload.What.MapEvent;
        RunSerialized(() =>
        {
            if (!objectManager.TryGetId(mapEvent, out var mapEventId))
                return;

            var joinerPeers = new List<NetPeer>();
            lock (pendingJoinersGate)
            {
                if (pendingJoiners.TryGetValue(mapEventId, out var joiners))
                    joinerPeers.AddRange(joiners.Values.Select(joiner => joiner.Peer));
                pendingJoiners.Remove(mapEventId);
            }
            conclusionRetries.Remove(mapEventId);
            keptHostDeadlines.Remove(mapEventId);
            completionTracker.Clear(mapEventId);
            EndMissionsOfFinalizedBattle(mapEvent, mapEventId, joinerPeers);
        });
    }

    // Something other than our own conclusion (peace, say) finalized the battle while players were in or joining its
    // mission. Our conclusion already holds the instance's fence, so winning it here tells the two apart.
    private void EndMissionsOfFinalizedBattle(MapEvent mapEvent, string mapEventId, List<NetPeer> joinerPeers)
    {
        bool hasMembers = missionManager.TryGetControllers(mapEventId, out var members);
        if (!hasMembers && joinerPeers.Count == 0)
            return;

        // Membership only changes on the game thread, so the members read above are the ones the fence checks.
        // With only pending joiners the battle may never have had an instance; its fence record then lasts until
        // ConclusionRetention expires and MaxConcludedInstances caps the records, so it does not leak.
        bool fenced = hasMembers
            ? missionManager.TryBeginActiveInstanceConclusion(mapEventId, members)
            : missionManager.TryBeginEmptyInstanceConclusion(mapEventId);
        if (!fenced)
        {
            Logger.Debug("Battle {Instance} was finalized while its conclusion held the fence; its missions end with that result",
                mapEventId);
            return;
        }

        // The fence now refuses late results and entries, and the members' later departures find no host to migrate.
        missionManager.CompleteInstanceConclusion(mapEventId, succeeded: true);
        hostRegistry.Remove(mapEventId);
        reserveBuilder.ForgetMapEvent(mapEvent);
        Logger.Warning("Battle {Instance} was finalized with {Members} player(s) in its mission and {Joiners} joining; ending their missions",
            mapEventId, members.Count, joinerPeers.Count);
        foreach (var controllerId in members)
        {
            if (playerManager.TryGetPeer(controllerId, out var peer))
                joinerPeers.Add(peer);
        }

        foreach (var peer in joinerPeers.Distinct())
            network.Send(peer, new NetworkEndBattleMission(mapEventId));
    }

    private void Handle_BattleStateChangeProcessed(MessagePayload<BattleStateChangeProcessed> payload)
    {
        var result = payload.What;
        RunSerialized(() =>
        {
            if (!missionManager.CompleteInstanceConclusion(result.MapEventId, result.Applied))
            {
                // The claim is gone. Only a claim that timed out is recovered here; anything else is a
                // stale result and stays ignored.
                if (missionManager.TryTakeExpiredConclusion(result.MapEventId))
                    RecoverStalledConclusion(result.MapEventId, DateTime.UtcNow);

                return;
            }

            if (!result.Applied)
            {
                RecoverStalledConclusion(result.MapEventId, DateTime.UtcNow + ConclusionRetryDelay);
                return;
            }

            conclusionRetries.Remove(result.MapEventId);
            keptHostDeadlines.Remove(result.MapEventId);
            lock (pendingJoinersGate)
                pendingJoiners.Remove(result.MapEventId);
        });
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        RunSerialized(() =>
        {
            var now = DateTime.UtcNow;
            var changedInstances = new HashSet<string>();
            lock (pendingJoinersGate)
            {
                foreach (var entry in pendingJoiners)
                {
                    var expired = entry.Value
                        .Where(joiner => now >= joiner.Value.ExpiresUtc)
                        .Select(joiner => joiner.Key)
                        .ToList();
                    foreach (var reservationId in expired)
                    {
                        var controllerId = entry.Value[reservationId].ControllerId;
                        entry.Value.Remove(reservationId);
                        Logger.Warning("Battle join reservation for {Controller} in {Instance} expired after {Timeout}",
                            controllerId, entry.Key, JoinReservationTimeout);
                    }

                    if (expired.Count > 0)
                        changedInstances.Add(entry.Key);
                }

                RemoveEmptyPendingInstances();
            }

            foreach (var instanceId in changedInstances)
                TryConcludeReportedBattle(instanceId);

            ExpireKeptHostDeadlines(now);

            foreach (var instanceId in missionManager.TakeExpiredConclusions())
                RecoverStalledConclusion(instanceId, now);

            var dueRetries = conclusionRetries
                .Where(entry => now >= entry.Value)
                .Select(entry => entry.Key)
                .ToList();
            foreach (var instanceId in dueRetries)
            {
                conclusionRetries.Remove(instanceId);
                TryConcludeReportedBattle(instanceId);
            }
        });
    }

    // A kept host never answers again, so a member that stays without matching its report would hold the battle open:
    // past the deadline the host's report concludes it alone (Q7), still behind any pending joiner.
    private void ExpireKeptHostDeadlines(DateTime now)
    {
        var expired = keptHostDeadlines.Where(entry => now >= entry.Value).Select(entry => entry.Key).ToList();
        foreach (var instanceId in expired)
        {
            keptHostDeadlines.Remove(instanceId);
            Logger.Warning("Battle {Instance} concludes with its kept host's report: its remaining members did not match it within {Timeout}",
                instanceId, KeptHostReportTimeout);
            completionTracker.StopWaitingForMembers(instanceId);
            TryConcludeReportedBattle(instanceId);
        }
    }

    private void RecoverStalledConclusion(string mapEventId, DateTime retryDueUtc)
    {
        if (!objectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent) ||
            mapEvent.IsFinalized ||
            mapEvent.BattleState == BattleState.AttackerVictory ||
            mapEvent.BattleState == BattleState.DefenderVictory)
        {
            conclusionRetries.Remove(mapEventId);
            keptHostDeadlines.Remove(mapEventId);
            lock (pendingJoinersGate)
                pendingJoiners.Remove(mapEventId);
            completionTracker.Clear(mapEventId);
            Logger.Warning("Battle conclusion for {Instance} was not applied because the map event is no longer active",
                mapEventId);
            return;
        }

        conclusionRetries[mapEventId] = retryDueUtc;
        Logger.Warning("Battle conclusion for {Instance} was not applied; reopening it for retry", mapEventId);
    }

    private void RemovePendingJoiner(string instanceId, string controllerId, Guid reservationId)
    {
        lock (pendingJoinersGate)
        {
            if (!pendingJoiners.TryGetValue(instanceId, out var joiners))
                return;

            if (reservationId != Guid.Empty)
            {
                if (joiners.TryGetValue(reservationId, out var reservation) &&
                    reservation.ControllerId == controllerId)
                {
                    joiners.Remove(reservationId);
                }
            }
            else
            {
                var controllerReservations = joiners
                    .Where(entry => entry.Value.ControllerId == controllerId)
                    .Select(entry => entry.Key)
                    .ToList();
                foreach (var controllerReservationId in controllerReservations)
                    joiners.Remove(controllerReservationId);
            }

            if (joiners.Count == 0)
                pendingJoiners.Remove(instanceId);
        }
    }

    // Only reservations made through the sending peer, so a refusal never releases a join another connection holds.
    private int RemovePeerReservations(string instanceId, string controllerId, NetPeer peer)
    {
        lock (pendingJoinersGate)
        {
            if (!pendingJoiners.TryGetValue(instanceId, out var joiners))
                return 0;

            var peerReservations = joiners
                .Where(entry => entry.Value.ControllerId == controllerId && ReferenceEquals(entry.Value.Peer, peer))
                .Select(entry => entry.Key)
                .ToList();
            foreach (var reservationId in peerReservations)
                joiners.Remove(reservationId);

            if (joiners.Count == 0)
                pendingJoiners.Remove(instanceId);

            return peerReservations.Count;
        }
    }

    private void RemoveEmptyPendingInstances()
    {
        var emptyInstances = pendingJoiners
            .Where(entry => entry.Value.Count == 0)
            .Select(entry => entry.Key)
            .ToList();
        foreach (var instanceId in emptyInstances)
            pendingJoiners.Remove(instanceId);
    }

    private void PublishConclusion(string instanceId, int memberCount, BattleState concludedState, int hostEpoch)
    {
        Logger.Information("All {Count} mission member(s) reconciled {State} for battle {Instance}; concluding at host epoch {Epoch}",
            memberCount, concludedState, instanceId, hostEpoch);
        if (concludedState == BattleState.DefenderPullBack)
        {
            messageBroker.Publish(this, new AuthoritativeSiegeAmbushCompletionRequested(instanceId));
            return;
        }

        messageBroker.Publish(this, new AuthoritativeBattleConclusionRequested(instanceId, concludedState, hostEpoch));
    }

    private void ConcludeAbandonedBattle(
        string instanceId,
        int memberCount,
        BattleState concludedState,
        int hostEpoch)
    {
        if (!missionManager.TryBeginEmptyInstanceConclusion(instanceId))
            return;

        hostRegistry.Remove(instanceId);
        PublishConclusion(instanceId, memberCount, concludedState, hostEpoch);
    }

    // Keep membership signals, host changes, and result decisions on one FIFO queue.
    private static void RunSerialized(Action action, [CallerMemberName] string context = null)
        => GameThread.RunSafe(action, context: context);
}
