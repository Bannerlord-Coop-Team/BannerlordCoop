using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArmyNeedsSupplies;

internal class ArmyNeedsSuppliesFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<ArmyNeedsSuppliesFinalizationProofStrategy>();

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest armyQuest))
        {
            return false;
        }

        switch ((ArmyNeedsSuppliesBranch)proof)
        {
            case ArmyNeedsSuppliesBranch.SuccessByGrain:
                return TryRun(armyQuest, proof, armyQuest.CheckIfPlayerCollectedGrain, armyQuest.CollectedGrainConsequence);
            case ArmyNeedsSuppliesBranch.SuccessByGrainAndLiveStock:
                return TryRun(armyQuest, proof, armyQuest.CheckIfPlayerCollectedGrainLiveStock, armyQuest.CollectedGrainAndLiveStockConsequence);
            case ArmyNeedsSuppliesBranch.SuccessByGrainAndWine:
                return TryRun(armyQuest, proof, armyQuest.CheckIfPlayerCollectedGrainWine, armyQuest.CollectedGrainAndWineConsequence);
            case ArmyNeedsSuppliesBranch.SuccessByEverything:
                return TryRun(armyQuest, proof, armyQuest.CheckIfPlayerCollectedEverything, armyQuest.CollectedEverythingConsequence);
            default:
                return false;
        }
    }

    private static bool TryRun(Quest quest, byte proof, Func<bool> hasCollected, Action consequence)
    {
        quest.CalculateAndUpdateRequestedItemsCountInPlayer(notifyPlayer: false);

        if (!hasCollected())
        {
            Logger.Warning("The owner of {quest} does not carry the supplies for branch {proof}", quest.StringId, proof);
            return true;
        }

        consequence();
        return true;
    }
}
