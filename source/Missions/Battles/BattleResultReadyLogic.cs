using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>Forwards the native result-ready callback to the coop battle result reporter.</summary>
public class BattleResultReadyLogic : MissionLogic
{
    private readonly IBattleResultCommitter resultCommitter;
    private readonly ISiegeEngineStateReporter siegeEngineStateReporter;

    public BattleResultReadyLogic(
        IBattleResultCommitter resultCommitter,
        ISiegeEngineStateReporter siegeEngineStateReporter)
    {
        this.resultCommitter = resultCommitter;
        this.siegeEngineStateReporter = siegeEngineStateReporter;
    }

    public override void OnMissionResultReady(MissionResult missionResult)
    {
        if (missionResult?.BattleResolved == true)
            siegeEngineStateReporter.ReportConcludedIfHost();

        resultCommitter.ReportResolvedResult(missionResult);
    }
}
