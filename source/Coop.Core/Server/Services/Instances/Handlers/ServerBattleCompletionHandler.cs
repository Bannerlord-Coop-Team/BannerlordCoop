using Common.Logging;
using Common.Messaging;
using Common.Network.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Missions.Messages;
using Serilog;
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
    private readonly Dictionary<string, Dictionary<string, NetPeer>> pendingJoiners = new();

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
        if (!missionManager.TryGetControllers(instanceId, out var currentMembers) ||
            !hostRegistry.TryGet(instanceId, out var assignment))
        {
            Logger.Information("Ignoring battle result report from {Controller} for inactive instance {Instance}",
                controllerId, instanceId);
            return;
        }

        var expectedReporters = IncludePendingJoiners(instanceId, currentMembers);

        if (!completionTracker.TryRecordResult(
                instanceId,
                controllerId,
                result.BattleState,
                expectedReporters,
                assignment.HostControllerId,
                out var concludedState))
        {
            return;
        }

        PublishConclusion(instanceId, expectedReporters.Count, concludedState, assignment.Epoch);
    }

    private void Handle_MissionMemberDeparted(MessagePayload<MissionMemberDeparted> payload)
    {
        completionTracker.RemoveMember(
            payload.What.InstanceId,
            payload.What.ControllerId,
            payload.What.IsInstanceEmpty);

        RemovePendingJoiner(payload.What.InstanceId, payload.What.ControllerId);

        if (!payload.What.IsInstanceEmpty)
            TryConcludeReportedBattle(payload.What.InstanceId);
    }

    private void Handle_BattleHostAssignmentChanged(MessagePayload<BattleHostAssignmentChanged> payload)
    {
        TryConcludeReportedBattle(payload.What.MapEventId);
    }

    private void TryConcludeReportedBattle(string instanceId)
    {
        if (!missionManager.TryGetControllers(instanceId, out var currentMembers) ||
            !hostRegistry.TryGet(instanceId, out var assignment))
        {
            return;
        }

        var expectedReporters = IncludePendingJoiners(instanceId, currentMembers);
        if (!completionTracker.TryReconcile(
                instanceId,
                expectedReporters,
                assignment.HostControllerId,
                out var concludedState))
        {
            return;
        }

        PublishConclusion(instanceId, expectedReporters.Count, concludedState, assignment.Epoch);
    }

    private IReadOnlyCollection<string> IncludePendingJoiners(
        string instanceId,
        IReadOnlyCollection<string> missionMembers)
    {
        var expectedReporters = new HashSet<string>(missionMembers);
        lock (pendingJoinersGate)
        {
            if (pendingJoiners.TryGetValue(instanceId, out var joiners))
                expectedReporters.UnionWith(joiners.Keys);
        }

        return expectedReporters;
    }

    private void Handle_BattleJoinAccepted(MessagePayload<BattleJoinAccepted> payload)
    {
        if (payload.Who is not NetPeer peer)
            return;

        lock (pendingJoinersGate)
        {
            if (!pendingJoiners.TryGetValue(payload.What.InstanceId, out var joiners))
            {
                joiners = new Dictionary<string, NetPeer>();
                pendingJoiners[payload.What.InstanceId] = joiners;
            }

            joiners[payload.What.ControllerId] = peer;
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
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        List<string> changedInstances = new List<string>();
        lock (pendingJoinersGate)
        {
            foreach (var entry in pendingJoiners)
            {
                var disconnected = entry.Value
                    .Where(joiner => ReferenceEquals(joiner.Value, payload.What.PlayerId))
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
}
