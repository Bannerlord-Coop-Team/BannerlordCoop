using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
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
    private readonly IMissionManager missionManager;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IBattleHostRegistry hostRegistry;
    private readonly IBattleCompletionTracker completionTracker;
    private readonly object pendingJoinersGate = new object();
    private readonly Dictionary<string, Dictionary<Guid, (string ControllerId, NetPeer Peer, DateTime ExpiresUtc)>> pendingJoiners = new();
    private readonly Dictionary<string, DateTime> conclusionRetries = new();

    internal TimeSpan JoinReservationTimeout { get; set; } = TimeSpan.FromMinutes(2);
    internal TimeSpan ConclusionRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    public ServerBattleCompletionHandler(
        IMessageBroker messageBroker,
        IMissionManager missionManager,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IBattleHostRegistry hostRegistry,
        IBattleCompletionTracker completionTracker)
    {
        this.messageBroker = messageBroker;
        this.missionManager = missionManager;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.hostRegistry = hostRegistry;
        this.completionTracker = completionTracker;

        messageBroker.Subscribe<NetworkBattleResultReady>(Handle_NetworkBattleResultReady);
        messageBroker.Subscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Subscribe<BattleHostAssignmentChanged>(Handle_BattleHostAssignmentChanged);
        messageBroker.Subscribe<BattleJoinAccepted>(Handle_BattleJoinAccepted);
        messageBroker.Subscribe<BattleJoinCancelled>(Handle_BattleJoinCancelled);
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

            bool canConclude = !HasPendingJoiners(instanceId);

            if (!completionTracker.TryRecordResult(
                    instanceId,
                    controllerId,
                    result.BattleState,
                    result.HostEpoch,
                    currentMembers,
                    assignment.HostControllerId,
                    assignment.Epoch,
                    out var concludedState,
                    canConclude))
            {
                return;
            }

            if (!missionManager.TryBeginActiveInstanceConclusion(instanceId, currentMembers))
                return;

            PublishConclusion(instanceId, currentMembers.Count, concludedState, assignment.Epoch);
        }
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
            }

            TryConcludeReportedBattle(mapEventId);
        });
    }

    private void TryConcludeReportedBattle(string instanceId)
    {
        lock (pendingJoinersGate)
        {
            bool hasPendingJoiners = HasPendingJoiners(instanceId);
            if (!missionManager.TryGetControllers(instanceId, out var currentMembers))
            {
                if (!hasPendingJoiners && completionTracker.TryConcludeAbandoned(
                        instanceId,
                        out var abandonedState,
                        out var abandonedEpoch,
                        out var abandonedMemberCount))
                {
                    QueueAbandonedConclusion(instanceId, abandonedMemberCount, abandonedState, abandonedEpoch);
                }

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
                    !hasPendingJoiners))
            {
                return;
            }

            if (!missionManager.TryBeginActiveInstanceConclusion(instanceId, currentMembers))
                return;

            PublishConclusion(instanceId, currentMembers.Count, concludedState, assignment.Epoch);
        }
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

    private void Handle_MissionMemberEntered(MessagePayload<MissionMemberEntered> payload)
    {
        var entry = payload.What;
        RunSerialized(() =>
        {
            completionTracker.ResetMember(
                entry.InstanceId,
                entry.ControllerId,
                entry.IsFirstMember);
            RemovePendingJoiner(entry.InstanceId, entry.ControllerId, Guid.Empty);

            if (entry.IsFirstMember ||
                !missionManager.TryGetControllers(entry.InstanceId, out var currentMembers) ||
                !hostRegistry.TryGet(entry.InstanceId, out var assignment) ||
                !completionTracker.TryAcceptAuthoritativeResultForMember(
                    entry.InstanceId,
                    entry.ControllerId,
                    currentMembers,
                    assignment.HostControllerId,
                    assignment.Epoch,
                    out var concludedState,
                    canConclude: !HasPendingJoiners(entry.InstanceId)) ||
                !missionManager.TryBeginActiveInstanceConclusion(entry.InstanceId, currentMembers))
            {
                return;
            }

            PublishConclusion(entry.InstanceId, currentMembers.Count, concludedState, assignment.Epoch);
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

            lock (pendingJoinersGate)
                pendingJoiners.Remove(mapEventId);
            conclusionRetries.Remove(mapEventId);
            completionTracker.Clear(mapEventId);
        });
    }

    private void Handle_BattleStateChangeProcessed(MessagePayload<BattleStateChangeProcessed> payload)
    {
        var result = payload.What;
        RunSerialized(() =>
        {
            if (!missionManager.CompleteInstanceConclusion(result.MapEventId, result.Applied))
                return;

            if (!result.Applied)
            {
                if (!objectManager.TryGetObject<MapEvent>(result.MapEventId, out var mapEvent) ||
                    mapEvent.IsFinalized ||
                    mapEvent.BattleState == BattleState.AttackerVictory ||
                    mapEvent.BattleState == BattleState.DefenderVictory)
                {
                    conclusionRetries.Remove(result.MapEventId);
                    lock (pendingJoinersGate)
                        pendingJoiners.Remove(result.MapEventId);
                    completionTracker.Clear(result.MapEventId);
                    Logger.Warning("Battle conclusion for {Instance} was not applied because the map event is no longer active",
                        result.MapEventId);
                    return;
                }

                conclusionRetries[result.MapEventId] = DateTime.UtcNow + ConclusionRetryDelay;
                Logger.Warning("Battle conclusion for {Instance} was not applied; reopening it for retry",
                    result.MapEventId);
                return;
            }

            conclusionRetries.Remove(result.MapEventId);
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
        messageBroker.Publish(this, new NetworkChangeBattleState(instanceId, concludedState, hostEpoch));
    }

    private void QueueAbandonedConclusion(
        string instanceId,
        int memberCount,
        BattleState concludedState,
        int hostEpoch)
    {
        RunSerialized(() =>
        {
            if (!missionManager.TryBeginEmptyInstanceConclusion(instanceId))
                return;

            hostRegistry.Remove(instanceId);
            PublishConclusion(instanceId, memberCount, concludedState, hostEpoch);
        });
    }

    // Keep membership signals, host changes, and result decisions on one FIFO queue.
    private static void RunSerialized(Action action, [CallerMemberName] string context = null)
        => GameThread.RunSafe(action, context: context);
}
