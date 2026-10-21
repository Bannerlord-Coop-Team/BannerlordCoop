using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.TroopRosters.Interfaces;

public interface IRecruitmentSideEffects : IGameAbstraction
{
    void Apply(MobileParty party, CharacterObject troop, int count);
}

public class RecruitmentSideEffects : IRecruitmentSideEffects
{
    public void Apply(MobileParty party, CharacterObject troop, int count)
    {
        var leaderHero = party.LeaderHero;
        if (leaderHero.GetPerkValue(DefaultPerks.Leadership.FamousCommander))
        {
            party.MemberRoster.AddXpToTroop(troop, (int)DefaultPerks.Leadership.FamousCommander.SecondaryBonus * count);
        }
        SkillLevelingManager.OnTroopRecruited(leaderHero, count, troop.Tier);
        if (troop.Occupation == Occupation.Bandit)
        {
            SkillLevelingManager.OnBanditsRecruited(party, troop, count);
        }
    }
}
