using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameDebug.Messages;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Forwards the native result-ready callback to the coop battle result reporter, and leaves the mission when the server
/// ends this battle before it has a result.
/// </summary>
public class BattleResultReadyLogic : MissionLogic
{
    private readonly IBattleResultCommitter resultCommitter;
    private readonly ISiegeEngineStateReporter siegeEngineStateReporter;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly INetwork relayNetwork;
    private bool hasResolvedMissionResult;

    public BattleResultReadyLogic(
        IBattleResultCommitter resultCommitter,
        ISiegeEngineStateReporter siegeEngineStateReporter,
        IMessageBroker messageBroker,
        IBattleSession session,
        IBattleDeploymentCoordinator deployment,
        INetwork relayNetwork)
    {
        this.resultCommitter = resultCommitter;
        this.siegeEngineStateReporter = siegeEngineStateReporter;
        this.messageBroker = messageBroker;
        this.session = session;
        this.deployment = deployment;
        this.relayNetwork = relayNetwork;

        messageBroker.Subscribe<BattleHostAuthorityAcquired>(Handle_BattleHostAuthorityAcquired);
        messageBroker.Subscribe<NetworkEndBattleMission>(Handle_NetworkEndBattleMission);
    }

    public override void OnMissionResultReady(MissionResult missionResult)
    {
        if (missionResult?.BattleResolved == true)
        {
            hasResolvedMissionResult = true;
            siegeEngineStateReporter.ReportConcludedIfHost();
        }

        resultCommitter.ReportResolvedResult(missionResult);
    }

    public override void OnEndMissionInternal()
    {
        messageBroker.Unsubscribe<BattleHostAuthorityAcquired>(Handle_BattleHostAuthorityAcquired);
        messageBroker.Unsubscribe<NetworkEndBattleMission>(Handle_NetworkEndBattleMission);
        base.OnEndMission();
    }

    private void Handle_BattleHostAuthorityAcquired(MessagePayload<BattleHostAuthorityAcquired> payload)
    {
        if (payload.What.MapEventId != session.InstanceId)
            return;

        GameThread.RunSafe(() =>
        {
            if (!session.IsLocalHost || !deployment.IsCommitted ||
                payload.What.MapEventId != session.InstanceId ||
                !resultCommitter.TryGetResolvedState(out _))
                return;

            siegeEngineStateReporter.ReportConcludedIfHost();
            resultCommitter.ReportAcceptedResult();
        }, context: nameof(Handle_BattleHostAuthorityAcquired));
    }

    // The server finalized this battle mid mission (a peace decision, say), so no result can be applied to it any more.
    // MissionEnded is already true while the scoreboard waits on other results, so skip only a mission tearing down.
    private void Handle_NetworkEndBattleMission(MessagePayload<NetworkEndBattleMission> payload)
    {
        if (payload.What.MapEventId != session.InstanceId)
            return;

        GameThread.RunSafe(() =>
        {
            if (payload.What.MapEventId != session.InstanceId ||
                Mission.CurrentState == Mission.State.EndingNextFrame ||
                Mission.CurrentState == Mission.State.Over)
                return;

            messageBroker.Publish(this, new SendInformationMessage("This battle has ended. Returning to the map."));
            Mission.EndMission();
        }, context: nameof(Handle_NetworkEndBattleMission));
    }

    // Only report retreats triggered by the retreat action
    public override void OnRetreatMission()
    {
#if DEBUG
        if (session.DebugWithholdCompletion) return;
#endif
        if (!hasResolvedMissionResult && session.HasInstance) relayNetwork.SendAll(new NetworkBattleRetreated(session.InstanceId));
    }
}
