using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Coop <see cref="BattleDeploymentMissionController"/> that holds the one-time team + player-command setup until
/// <see cref="CoopBattleMissionSpawnHandler"/> has sized the sides. The native <see cref="DeploymentMissionController"/>
/// runs <c>SetupTeams</c> on the first tick where the scene exists, with no troop-count gate. <c>SetupTeams</c> drives
/// <c>OnSetupTeamsOfSide -&gt; DefaultBattleMissionAgentSpawnLogic.OnSideDeploymentOver -&gt; Mission.OnTeamDeployed -&gt;
/// AssignPlayerRoleInTeamMissionController.OnTeamDeployed</c>, the sole grant of player command during deployment: the
/// spawn logic's own re-fire is dead while <c>Mission.Mode == Deployment</c> because its <c>IsDeploymentOver</c> guard
/// is always false there. In coop the owned reserve can land after that first tick, so the setup would run against
/// empty teams with a null <c>Agent.Main</c> and latch the player commanding nothing.
/// <para>
/// Gating the tick on the spawn handler's <see cref="CoopBattleMissionSpawnHandler.IsSized"/> defers <c>SetupTeams</c>
/// to the tick the handler jointly sized the sides. On that same tick the handler (earlier in the behavior list) has
/// already re-run Init, so <c>SetupTeams -&gt; OnSetupTeamsOfSide(PlayerSide)</c> spawns our troops and hero — the hero
/// becomes <c>Agent.Main</c> inside that synchronous <c>SetSpawnTroops(enforceSpawning)</c> call — before
/// <c>OnTeamDeployed</c> grants command over the now-populated player formations. The on-time path is unchanged: the
/// handler sizes in <c>AfterStart</c>, so the gate is already open on the first tick.
/// </para>
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
        // Hold the native team/command setup until the handler has sized the sides. Gating on the handler's IsSized
        // (set on the game thread after Init) rather than the suppliers' IsPopulated keeps setup strictly after
        // sizing: IsPopulated flips on the network thread and could read true between the handler's tick and ours in
        // the same frame, running SetupTeams against still-empty phases and latching the player onto no units.
        if (_spawnHandler != null && !_spawnHandler.IsSized) return;

        base.OnMissionTick(dt);
    }
}
