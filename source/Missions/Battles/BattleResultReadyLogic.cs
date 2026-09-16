using Common;
using Common.Messaging;
using Common.Network;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>Forwards the native result-ready callback to the coop battle result reporter.</summary>
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

    // Only report retreats triggered by the retreat action
    public override void OnRetreatMission()
    {
        if (!hasResolvedMissionResult && session.HasInstance) relayNetwork.SendAll(new NetworkBattleRetreated(session.InstanceId));
    }
}
