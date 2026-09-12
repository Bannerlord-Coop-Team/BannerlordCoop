using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Coop <see cref="BattleDeploymentMissionController"/> that defers the native one-time team/command setup
/// (<c>SetupTeams</c>, which grants player command via <c>OnTeamDeployed</c>) until the spawn handler has sized the
/// sides. Native runs it on the first tick regardless of troops; in coop a late reserve would run it against empty
/// teams with a null <c>Agent.Main</c> and latch the player commanding nothing. Gating on
/// <see cref="CoopBattleMissionSpawnHandler.IsSized"/> holds it until the troops and hero have spawned; the on-time
/// path is unchanged (IsSized is already true on the first tick).
/// </summary>
public class CoopBattleDeploymentMissionController : BattleDeploymentMissionController
{
    private CoopBattleMissionSpawnHandler _spawnHandler;

    public CoopBattleDeploymentMissionController(bool isPlayerAttacker)
        : base(isPlayerAttacker)
    {
    }

    public override void OnBehaviorInitialize()
    {
        base.OnBehaviorInitialize();
        _spawnHandler = base.Mission.GetMissionBehavior<CoopBattleMissionSpawnHandler>();
    }

    public override void OnMissionTick(float dt)
    {
        // Hold SetupTeams until the sides are sized. Gate on the handler's game-thread IsSized, not the suppliers'
        // network-thread IsPopulated (which could read true mid-frame before Init has sized). The handler bounds the
        // wait with its own deadline: a usable partial reserve latches IsSized, while an unusable 0/0 reserve ends
        // the mission through its normal lifecycle instead of allowing SetupTeams to commit an empty deployment.
        if (_spawnHandler != null && !_spawnHandler.IsSized) return;

        base.OnMissionTick(dt);
    }
}
