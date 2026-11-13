using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Common.Logging;
using Serilog;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal class CaravanAmbushFinalizationProofStrategy : IFinalizationProofStrategy, IOwnerPartyRequiredFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravanAmbushFinalizationProofStrategy>();

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest caravan) || proof != (byte)CaravanAmbushBranch.CollectReward) return false;
        if (!caravan.IsOngoing || !caravan._isCaravanSaved || caravan._caravanParty == null)
        {
            Logger.Warning("Skipping late caravan reward for quest {QuestId}", caravan.StringId);
            return true;
        }
        caravan.OnQuestSucceeded();
        return true;
    }
}
