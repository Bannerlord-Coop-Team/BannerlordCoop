using System;
using TaleWorlds.Core;
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
    private readonly ICoopDeploymentPlanBuilder deploymentPlanBuilder;
    private CoopBattleMissionSpawnHandler _spawnHandler;

    public CoopBattleDeploymentMissionController(
        bool isPlayerAttacker,
        ICoopDeploymentPlanBuilder deploymentPlanBuilder)
        : base(isPlayerAttacker)
    {
        if (deploymentPlanBuilder == null) throw new ArgumentNullException(nameof(deploymentPlanBuilder));

        this.deploymentPlanBuilder = deploymentPlanBuilder;
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

    public override void OnSetupTeamsOfSide(BattleSideEnum battleSide)
    {
        // The first check reserves this client's troops and builds plans for teams with locally supplied origins.
        // Foreign teams have no local origins, but OnTeamDeployed still expects their real plan to exist.
        MissionAgentSpawnLogic.SetSpawnTroops(battleSide, spawnTroops: true, enforceSpawning: true);

        deploymentPlanBuilder.EnsurePlans(base.Mission, MissionAgentSpawnLogic, battleSide);

        // Plan creation happened after the first check's spawn gate, so rerun it before announcing deployment.
        // This is what turns the already-reserved local origins into agents and sets Mission.InitialPlayerAgent.
        MissionAgentSpawnLogic.SetSpawnTroops(battleSide, spawnTroops: true, enforceSpawning: true);

        if (battleSide == PlayerSide && base.Mission.InitialPlayerAgent == null)
        {
            throw new InvalidOperationException(
                $"Player-side deployment completed without spawning the initial player agent ({PlayerSide}).");
        }

        SetupAgentAIStatesForSide(battleSide);
        MissionAgentSpawnLogic.OnSideDeploymentOver(battleSide);
    }

}
