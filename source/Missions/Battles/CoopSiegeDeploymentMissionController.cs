using GameInterface.Services.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Coop <see cref="SiegeDeploymentMissionController"/> with the same sizing gate as
/// <see cref="CoopBattleDeploymentMissionController"/>: the native one-time team/command setup runs on
/// the first tick regardless of troops, but in coop a late reserve would run it against empty teams.
/// Everything siege-specific (deployment points, AI weapon deployment, ladder disabling,
/// reinforcement re-enable) is inherited.
/// </summary>
public class CoopSiegeDeploymentMissionController : SiegeDeploymentMissionController
{
    private CoopBattleMissionSpawnHandler _spawnHandler;

    public CoopSiegeDeploymentMissionController(bool isPlayerAttacker)
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

    private const float AuthorityWaitDeadline = 15f;
    private float _authorityWait;
    private bool _deployedWithoutAuthority;
}
