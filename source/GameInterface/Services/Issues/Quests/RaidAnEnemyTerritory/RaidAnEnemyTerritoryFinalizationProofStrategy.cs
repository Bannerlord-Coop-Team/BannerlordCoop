using Common.Logging;
using Serilog;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;

internal sealed class RaidAnEnemyTerritoryFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<RaidAnEnemyTerritoryFinalizationProofStrategy>();

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest raidQuest) || (RaidAnEnemyTerritoryBranch)proof != RaidAnEnemyTerritoryBranch.SuccessfulRaid)
        {
            return false;
        }

        if (raidQuest._raidedVillages.Count < 3)
        {
            Logger.Warning("The owner of {quest} has not completed three raids for branch {proof}", raidQuest.StringId, proof);
            return true;
        }

        raidQuest.MainHeroRaidedAllVillages();
        return true;
    }
}
