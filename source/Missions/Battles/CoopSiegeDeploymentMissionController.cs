using System;
using GameInterface.Services.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Coop siege deployment waits for troop sizing and host election. Its side setup mirrors vanilla so foreign
/// teams receive real deployment plans before the spawn gate reruns; keep that sequence aligned with
/// <see cref="SiegeDeploymentMissionController"/>.
/// </summary>
public class CoopSiegeDeploymentMissionController : SiegeDeploymentMissionController
{
    private readonly ICoopDeploymentPlanBuilder deploymentPlanBuilder;
    private CoopBattleMissionSpawnHandler _spawnHandler;

    public CoopSiegeDeploymentMissionController(
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
        // Hold SetupTeams until the sides are sized, matching CoopBattleDeploymentMissionController.
        if (_spawnHandler != null && !_spawnHandler.IsSized) return;

        // Also hold until the host election result is known: SetupTeams runs the one-shot siege
        // engine auto-deploys, which the deployment patches suppress on non-authority clients — an
        // unknown-authority run would suppress them everywhere. Bounded so a lost election reply
        // cannot stall the mission in deployment forever.
        _authorityWait += dt;
        if (!SiegeMissionAuthorityGate.IsAuthorityKnown && _authorityWait < AuthorityWaitDeadline) return;

        if (!SiegeMissionAuthorityGate.IsAuthorityKnown) _deployedWithoutAuthority = true;

        // The deadline path ran the one-shot auto-deploys suppressed everywhere; when the election
        // result lands late, the authority re-runs them once (still in deployment, so the placements
        // replicate normally) instead of leaving the siege engineless.
        if (_deployedWithoutAuthority && SiegeMissionAuthorityGate.IsAuthorityKnown)
        {
            _deployedWithoutAuthority = false;
            if (SiegeMissionAuthorityGate.IsLocalAuthority && Mission.Mode == MissionMode.Deployment)
            {
                _siegeDeploymentHandler.DeployAllSiegeWeaponsOfPlayer();
                _siegeDeploymentHandler.DeployAllSiegeWeaponsOfAi();
            }
        }

        base.OnMissionTick(dt);
    }

    public override void OnSetupTeamsOfSide(BattleSideEnum battleSide)
    {
        foreach (var sideTeam in base.Mission.Teams)
        {
            if (sideTeam.Side == battleSide &&
                sideTeam.GeneralAgent != null &&
                sideTeam.GeneralAgent != base.Mission.InitialPlayerAgent)
            {
                sideTeam.GeneralAgent.SetDetachableFromFormation(value: false);
            }
        }

        Team team = battleSide == BattleSideEnum.Attacker
            ? base.Mission.AttackerTeam
            : base.Mission.DefenderTeam;
        if (team == base.Mission.PlayerTeam)
        {
            _siegeDeploymentHandler.RemoveUnavailableDeploymentPoints(battleSide);
            _siegeDeploymentHandler.UnHideDeploymentPoints(battleSide);
            _siegeDeploymentHandler.DeployAllSiegeWeaponsOfPlayer();
        }
        else
        {
            _siegeDeploymentHandler.DeployAllSiegeWeaponsOfAi();
        }

        MissionAgentSpawnLogic.SetSpawnTroops(battleSide, spawnTroops: true, enforceSpawning: true);
        deploymentPlanBuilder.EnsurePlans(base.Mission, MissionAgentSpawnLogic, battleSide);
        MissionAgentSpawnLogic.SetSpawnTroops(battleSide, spawnTroops: true, enforceSpawning: true);

        if (battleSide == PlayerSide && base.Mission.InitialPlayerAgent == null)
        {
            throw new InvalidOperationException(
                $"Player-side siege deployment completed without spawning the initial player agent ({PlayerSide}).");
        }

        foreach (WeakGameEntity entity in base.Mission.GetActiveEntitiesWithScriptComponentOfType<SiegeWeapon>())
        {
            SiegeWeapon siegeWeapon = entity.GetFirstScriptOfType<SiegeWeapon>();
            if (siegeWeapon != null && siegeWeapon.GetSide() == battleSide)
                siegeWeapon.TickAuxForInit();
        }

        SetupAgentAIStatesForSide(battleSide);
        if (team == base.Mission.PlayerTeam)
        {
            foreach (var formation in team.FormationsIncludingEmpty)
                formation.SetControlledByAI(isControlledByAI: true);
        }

        MissionAgentSpawnLogic.OnSideDeploymentOver(team.Side);
    }

    private const float AuthorityWaitDeadline = 15f;
    private float _authorityWait;
    private bool _deployedWithoutAuthority;
}
