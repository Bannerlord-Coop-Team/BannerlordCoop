using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.Players;
using LiteNetLib;
using Missions.Messages;
using Serilog;
using TaleWorlds.Core;

namespace Coop.Core.Server.Services.Instances.Handlers;

/// <summary>Concludes a shared battle once every current mission member reports the host's result.</summary>
public class ServerBattleCompletionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerBattleCompletionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IMissionManager missionManager;
    private readonly IPlayerManager playerManager;
    private readonly IBattleHostRegistry hostRegistry;
    private readonly IBattleCompletionTracker completionTracker;

    public ServerBattleCompletionHandler(
        IMessageBroker messageBroker,
        IMissionManager missionManager,
        IPlayerManager playerManager,
        IBattleHostRegistry hostRegistry,
        IBattleCompletionTracker completionTracker)
    {
        this.messageBroker = messageBroker;
        this.missionManager = missionManager;
        this.playerManager = playerManager;
        this.hostRegistry = hostRegistry;
        this.completionTracker = completionTracker;

        messageBroker.Subscribe<NetworkBattleResultReady>(Handle_NetworkBattleResultReady);
        messageBroker.Subscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Subscribe<BattleHostAssignmentChanged>(Handle_BattleHostAssignmentChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleResultReady>(Handle_NetworkBattleResultReady);
        messageBroker.Unsubscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Unsubscribe<BattleHostAssignmentChanged>(Handle_BattleHostAssignmentChanged);
    }

    private void Handle_NetworkBattleResultReady(MessagePayload<NetworkBattleResultReady> payload)
    {
        if (payload.Who is not NetPeer peer || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Warning("Ignoring battle result report for {Instance}: sender has no registered player",
                payload.What.InstanceId);
            return;
        }

        var instanceId = payload.What.InstanceId;
        if (!missionManager.TryGetControllers(instanceId, out var currentMembers) ||
            !hostRegistry.TryGet(instanceId, out var assignment))
        {
            Logger.Information("Ignoring battle result report from {Controller} for inactive instance {Instance}",
                player.ControllerId, instanceId);
            return;
        }

        if (!completionTracker.TryRecordResult(
                instanceId,
                player.ControllerId,
                payload.What.BattleState,
                currentMembers,
                assignment.HostControllerId,
                out var concludedState))
        {
            return;
        }

        PublishConclusion(instanceId, currentMembers.Count, concludedState, assignment.Epoch);
    }

    private void Handle_MissionMemberDeparted(MessagePayload<MissionMemberDeparted> payload)
    {
        completionTracker.RemoveMember(
            payload.What.InstanceId,
            payload.What.ControllerId,
            payload.What.IsInstanceEmpty);

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
            !hostRegistry.TryGet(instanceId, out var assignment) ||
            !completionTracker.TryReconcile(
                instanceId,
                currentMembers,
                assignment.HostControllerId,
                out var concludedState))
        {
            return;
        }

        PublishConclusion(instanceId, currentMembers.Count, concludedState, assignment.Epoch);
    }

    private void PublishConclusion(string instanceId, int memberCount, BattleState concludedState, int hostEpoch)
    {
        Logger.Information("All {Count} mission member(s) reported {State} for battle {Instance}; concluding at host epoch {Epoch}",
            memberCount, concludedState, instanceId, hostEpoch);
        messageBroker.Publish(this, new NetworkChangeBattleState(instanceId, concludedState, hostEpoch));
    }
}
