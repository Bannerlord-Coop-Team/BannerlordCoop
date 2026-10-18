using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.TroopRosters.Interfaces;

public interface IUnitRecruitedEffects
{
    /// <summary>
    /// Replaces the Hero.MainHero and MobileParty.MainParty in
    /// RecruitmentCampaignBehavior.OnUnitRecruited with the
    /// supplied leader and mobileparty
    /// </summary>
    void Apply(Hero leader, MobileParty party, CharacterObject troop, int count, bool hasPerk);
}

internal class UnitRecruitedEffects : IUnitRecruitedEffects
{
    public void Apply(Hero leader, MobileParty party, CharacterObject troop, int count, bool hasPerk)
    {
        if (hasPerk)
        {
            party.MemberRoster.AddXpToTroop(troop, (int)DefaultPerks.Leadership.FamousCommander.SecondaryBonus * count);
        }
        SkillLevelingManager.OnTroopRecruited(leader, count, troop.Tier);
        if (troop.Occupation == Occupation.Bandit)
        {
            SkillLevelingManager.OnBanditsRecruited(party, troop, count);
        }
    }
}
