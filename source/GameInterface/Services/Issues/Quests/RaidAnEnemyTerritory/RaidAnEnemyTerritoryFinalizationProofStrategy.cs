using Common.Logging;
using Serilog;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
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

        if (raidQuest._raidedVillages.Count < RaidAnEnemyTerritoryIssueBehavior.TargetRaidVillageCount)
        {
            Logger.Warning("Skipping the raid reward for {quest}: progress={progress}/{required}", raidQuest.StringId,
                raidQuest._raidedVillages.Count, RaidAnEnemyTerritoryIssueBehavior.TargetRaidVillageCount);
            return true;
        }

        raidQuest.MainHeroRaidedAllVillages();
        return true;
    }
}
