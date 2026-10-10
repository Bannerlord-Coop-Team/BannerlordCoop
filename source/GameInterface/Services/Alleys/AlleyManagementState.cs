using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Alleys;

public sealed class AlleyManagementState
{
    public string OverseerId { get; }
    public TroopRosterElementData[] Garrison { get; }
    public string UnderAttackByAlleyId { get; }
    public CampaignTime AttackResponseDueDate { get; }
    public long LastRecruitTimeTicks { get; }

    public AlleyManagementState(
        string overseerId,
        TroopRosterElementData[] garrison,
        string underAttackByAlleyId,
        CampaignTime attackResponseDueDate,
        long lastRecruitTimeTicks)
    {
        OverseerId = overseerId;
        Garrison = garrison;
        UnderAttackByAlleyId = underAttackByAlleyId;
        AttackResponseDueDate = attackResponseDueDate;
        LastRecruitTimeTicks = lastRecruitTimeTicks;
    }
}
