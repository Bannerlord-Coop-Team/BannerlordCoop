using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// The walls end logic with vanilla's defender pull-back armed. Vanilla stops reading the defenders' retreat once the
/// pull-back is armed, so a full rout would only end the walls after every fleeing defender left the scene. Once
/// enough defenders routed for the pull-back, a defender side that keeps running away ends the walls into the keep.
/// </summary>
internal sealed class CoopSiegeWallsEndLogic : BattleEndLogic
{
    private float lastDefenderStandTime;
    private bool pullBackAccepted;

    public CoopSiegeWallsEndLogic(int defenderTroopsForPullBack)
    {
        EnableEnemyDefenderPullBack(defenderTroopsForPullBack);
    }

    /// <summary>Ends the walls with the pull-back the host's result reported, whatever this client's defenders do.</summary>
    public void AcceptPullBack() => pullBackAccepted = true;

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (!DefendersAllRunningAway()) lastDefenderStandTime = Mission.CurrentTime;
    }

    public override bool MissionEnded(ref MissionResult missionResult)
    {
        // The same wait vanilla gives a retreating side before it counts as fled.
        var retreatWait = _missionAgentSpawnLogic.GetReinforcementInterval(BattleSideEnum.Defender) + 3f;
        bool defendersFled = _canCheckForEndCondition && _isEnemyDefenderPulledBack &&
            Mission.CurrentTime - lastDefenderStandTime > retreatWait;
        if (!pullBackAccepted && !defendersFled) return base.MissionEnded(ref missionResult);

        missionResult = MissionResult.CreateDefenderPushedBack();
        _missionAgentSpawnLogic.StopSpawner(BattleSideEnum.Attacker);
        _missionAgentSpawnLogic.StopSpawner(BattleSideEnum.Defender);
        return true;
    }

    private bool DefendersAllRunningAway()
    {
        var defenders = Mission.Teams.Where(team => team.Side == BattleSideEnum.Defender)
            .SelectMany(team => team.ActiveAgents).Where(agent => agent.IsHuman).ToList();
        return defenders.Count > 0 && defenders.All(agent => agent.IsRunningAway);
    }
}
