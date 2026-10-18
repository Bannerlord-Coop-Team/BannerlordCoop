using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;

internal sealed class RaidAnEnemyTerritoryFinalizationProofStrategy : IFinalizationProofStrategy
{
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest raidQuest) || (RaidAnEnemyTerritoryBranch)proof != RaidAnEnemyTerritoryBranch.SuccessfulRaid)
        {
            return false;
        }

        if (raidQuest._raidedVillages.Count < 3)
        {
            return true;
        }

        raidQuest.MainHeroRaidedAllVillages();
        return true;
    }
}
