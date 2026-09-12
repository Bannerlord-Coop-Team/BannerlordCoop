using System;
using SandBox.Tournaments.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Tournaments;

public delegate void TournamentHitProgressionRecorder(
    Agent affectedAgent,
    Agent affectorAgent,
    WeaponComponentData attackerWeapon,
    in Blow blow,
    in AttackCollisionData collisionData,
    float shotDifficulty);

public delegate void TournamentGuardReactionRecorder(
    Agent affectedAgent,
    Agent affectorAgent,
    bool isBlocked,
    in Blow blow,
    in AttackCollisionData collisionData);

/// <summary>Native fight rules with result authority handled by the coop tournament controller.</summary>
public class CoopTournamentFightMissionController : TournamentFightMissionController
{
    private Func<bool> shouldProcessAgentRemoval = () => true;
    private Func<bool> leaveAllowedProvider = () => false;
    private TournamentHitProgressionRecorder hitProgressionRecorder;
    private TournamentGuardReactionRecorder guardReactionRecorder;

    public CoopTournamentFightMissionController(CultureObject culture)
        : base(culture)
    {
    }

    public void SetAgentRemovalProvider(Func<bool> provider)
    {
        shouldProcessAgentRemoval = provider ?? (() => true);
    }

    public void SetLeaveAllowedProvider(Func<bool> provider)
    {
        leaveAllowedProvider = provider ?? (() => false);
    }

    public void SetHitProgressionRecorder(TournamentHitProgressionRecorder recorder)
    {
        hitProgressionRecorder = recorder;
    }

    public void SetGuardReactionRecorder(
        TournamentGuardReactionRecorder recorder)
    {
        guardReactionRecorder = recorder;
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
        guardReactionRecorder?.Invoke(
            affectedAgent,
            affectorAgent,
            isBlocked,
            in blow,
            in collisionData);
        hitProgressionRecorder?.Invoke(
            affectedAgent,
            affectorAgent,
            attackerWeapon,
            in blow,
            in collisionData,
            shotDifficulty);
    }

    public override InquiryData OnEndMissionRequest(out bool canPlayerLeave)
    {
        canPlayerLeave = leaveAllowedProvider?.Invoke() ?? false;
        return null;
    }
}
