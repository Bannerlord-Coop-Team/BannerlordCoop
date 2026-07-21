using Common;
using Common.Messaging;
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
    private volatile MissionResult resolvedResult;

    public BattleResultReadyLogic(
        IBattleResultCommitter resultCommitter,
        ISiegeEngineStateReporter siegeEngineStateReporter,
        IMessageBroker messageBroker,
        IBattleSession session)
    {
        this.resultCommitter = resultCommitter;
        this.siegeEngineStateReporter = siegeEngineStateReporter;
        this.messageBroker = messageBroker;
        this.session = session;

        messageBroker.Subscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
    }

    public override void OnMissionResultReady(MissionResult missionResult)
    {
        if (missionResult?.BattleResolved == true)
        {
            resolvedResult = missionResult;
            siegeEngineStateReporter.ReportConcludedIfHost();
        }

        resultCommitter.ReportResolvedResult(missionResult);
    }

    public override void OnEndMissionInternal()
    {
        messageBroker.Unsubscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
        base.OnEndMission();
    }

    private void Handle_BattleHostMigrated(MessagePayload<BattleHostMigrated> payload)
    {
        if (resolvedResult == null || payload.What.MapEventId != session.InstanceId)
            return;

        GameThread.RunSafe(() =>
        {
            if (!session.IsLocalHost || payload.What.MapEventId != session.InstanceId)
                return;

            siegeEngineStateReporter.ReportConcludedIfHost();
            resultCommitter.ReportResolvedResult(resolvedResult);
        }, context: nameof(Handle_BattleHostMigrated));
    }
}
