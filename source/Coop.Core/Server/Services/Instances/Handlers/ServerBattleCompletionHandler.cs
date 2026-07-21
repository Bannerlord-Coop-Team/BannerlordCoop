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
    private readonly Dictionary<string, Dictionary<string, (NetPeer Peer, DateTime ExpiresUtc)>> pendingJoiners = new();

    internal TimeSpan JoinReservationTimeout { get; set; } = TimeSpan.FromMinutes(2);

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
        messageBroker.Subscribe<NetworkMissionEntered>(Handle_NetworkMissionEntered);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleResultReady>(Handle_NetworkBattleResultReady);
        messageBroker.Unsubscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Unsubscribe<BattleHostAssignmentChanged>(Handle_BattleHostAssignmentChanged);
        messageBroker.Unsubscribe<BattleJoinAccepted>(Handle_BattleJoinAccepted);
        messageBroker.Unsubscribe<BattleJoinCancelled>(Handle_BattleJoinCancelled);
        messageBroker.Unsubscribe<NetworkMissionEntered>(Handle_NetworkMissionEntered);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
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

        TryRecordBattleResult(player.ControllerId, payload.What);
    }

    private void TryRecordBattleResult(string controllerId, NetworkBattleResultReady result)
    {
        var instanceId = result.InstanceId;
        if (!missionManager.TryGetControllers(instanceId, out var currentMembers))
        {
            Logger.Information("Ignoring battle result report from {Controller} for inactive instance {Instance}",
                controllerId, instanceId);
            return;
        }

        hostRegistry.TryGet(instanceId, out var assignment);
        bool canConclude = !HasPendingJoiners(instanceId);

        if (!completionTracker.TryRecordResult(
                instanceId,
                controllerId,
                result.BattleState,
                result.HostEpoch,
                currentMembers,
                assignment?.HostControllerId,
                assignment?.Epoch ?? 0,
                out var concludedState,
                canConclude))
        {
            return;
        }

        PublishConclusion(instanceId, currentMembers.Count, concludedState, assignment.Epoch);
    }

    private void Handle_MissionMemberDeparted(MessagePayload<MissionMemberDeparted> payload)
    {
        RemovePendingJoiner(payload.What.InstanceId, payload.What.ControllerId);
        TryConcludeReportedBattle(payload.What.InstanceId);
    }

    private void Handle_BattleHostAssignmentChanged(MessagePayload<BattleHostAssignmentChanged> payload)
    {
        TryConcludeReportedBattle(payload.What.MapEventId);
    }

    private void TryConcludeReportedBattle(string instanceId)
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

        PublishConclusion(instanceId, currentMembers.Count, concludedState, assignment.Epoch);
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

        lock (pendingJoinersGate)
        {
            if (!pendingJoiners.TryGetValue(payload.What.InstanceId, out var joiners))
            {
                joiners = new Dictionary<string, (NetPeer, DateTime)>();
                pendingJoiners[payload.What.InstanceId] = joiners;
            }

            joiners[payload.What.ControllerId] = (peer, DateTime.UtcNow + JoinReservationTimeout);
        }
    }

    private void Handle_BattleJoinCancelled(MessagePayload<BattleJoinCancelled> payload)
    {
        RemovePendingJoiner(payload.What.InstanceId, payload.What.ControllerId);
        TryConcludeReportedBattle(payload.What.InstanceId);
    }

    private void Handle_NetworkMissionEntered(MessagePayload<NetworkMissionEntered> payload)
    {
        RemovePendingJoiner(payload.What.InstanceId, payload.What.ControllerId);

        bool isFirstMember = missionManager.TryGetControllers(payload.What.InstanceId, out var members) &&
            members.Count == 1;
        completionTracker.ResetMember(payload.What.InstanceId, payload.What.ControllerId, isFirstMember);
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        List<string> changedInstances = new List<string>();
        lock (pendingJoinersGate)
        {
            foreach (var entry in pendingJoiners)
            {
                var disconnected = entry.Value
                    .Where(joiner => ReferenceEquals(joiner.Value.Peer, payload.What.PlayerId))
                    .Select(joiner => joiner.Key)
                    .ToList();
                foreach (var controllerId in disconnected)
                    entry.Value.Remove(controllerId);
                if (disconnected.Count > 0)
                    changedInstances.Add(entry.Key);
            }

            RemoveEmptyPendingInstances();
        }

        foreach (var instanceId in changedInstances)
            TryConcludeReportedBattle(instanceId);
    }

    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload)
    {
        if (!objectManager.TryGetId(payload.What.MapEvent, out var mapEventId))
            return;

        lock (pendingJoinersGate)
            pendingJoiners.Remove(mapEventId);
        completionTracker.Clear(mapEventId);
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
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
                foreach (var controllerId in expired)
                {
                    entry.Value.Remove(controllerId);
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
    }

    private void RemovePendingJoiner(string instanceId, string controllerId)
    {
        lock (pendingJoinersGate)
        {
            if (!pendingJoiners.TryGetValue(instanceId, out var joiners))
                return;

            joiners.Remove(controllerId);
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
        lock (pendingJoinersGate)
            pendingJoiners.Remove(instanceId);

        Logger.Information("All {Count} mission member(s) reported {State} for battle {Instance}; concluding at host epoch {Epoch}",
            memberCount, concludedState, instanceId, hostEpoch);
        messageBroker.Publish(this, new NetworkChangeBattleState(instanceId, concludedState, hostEpoch));
    }

    private void QueueAbandonedConclusion(
        string instanceId,
        int memberCount,
        BattleState concludedState,
        int hostEpoch)
    {
        GameThread.RunSafe(() =>
        {
            if (missionManager.TryGetControllers(instanceId, out _))
                return;

            hostRegistry.Remove(instanceId);
            PublishConclusion(instanceId, memberCount, concludedState, hostEpoch);
        }, context: nameof(QueueAbandonedConclusion));
    }
}
