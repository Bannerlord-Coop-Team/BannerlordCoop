using System;
using SandBox.Tournaments.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Tournaments;

public delegate void TournamentHitProgressionRecorder(
    Agent affectedAgent,
    Agent affectorAgent,
    WeaponComponentData attackerWeapon,
    in Blow blow,
    in AttackCollisionData collisionData,
    float shotDifficulty);

/// <summary>Native fight rules with result authority handled by the coop tournament controller.</summary>
public class CoopTournamentFightMissionController : TournamentFightMissionController
{
    private Func<bool> shouldProcessAgentRemoval = () => true;
    private TournamentHitProgressionRecorder hitProgressionRecorder;

    public CoopTournamentFightMissionController(CultureObject culture)
        : base(culture)
    {
    }

    public void SetAgentRemovalProvider(Func<bool> provider)
    {
        shouldProcessAgentRemoval = provider ?? (() => true);
    }

    public void SetHitProgressionRecorder(TournamentHitProgressionRecorder recorder)
    {
        hitProgressionRecorder = recorder;
    }

    public override void OnAgentRemoved(
        Agent affectedAgent,
        Agent affectorAgent,
        AgentState agentState,
        KillingBlow killingBlow)
    {
        if (!shouldProcessAgentRemoval()) return;
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);
    }

    public override void OnScoreHit(
        Agent affectedAgent,
        Agent affectorAgent,
        WeaponComponentData attackerWeapon,
        bool isBlocked,
        bool isSiegeEngineHit,
        in Blow blow,
        in AttackCollisionData collisionData,
        float damagedHp,
        float hitDistance,
        float shotDifficulty)
    {
        if (affectorAgent?.IsMount == true && affectorAgent.RiderAgent != null)
            affectorAgent = affectorAgent.RiderAgent;
        hitProgressionRecorder?.Invoke(
            affectedAgent,
            affectorAgent,
            attackerWeapon,
            in blow,
            in collisionData,
            shotDifficulty);
    }
}
