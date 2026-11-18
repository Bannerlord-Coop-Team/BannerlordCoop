using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Common.Logging;
using Serilog;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal class CaravanAmbushFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravanAmbushFinalizationProofStrategy>();

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest caravan) || proof != (byte)CaravanAmbushBranch.CollectReward) return false;
        if (Hero.MainHero.PartyBelongedTo == null)
        {
            Logger.Warning("Owner has no party for caravan reward {QuestId}", caravan.StringId);
            return true;
        }
        if (!caravan.IsOngoing || !caravan._isCaravanSaved || caravan._caravanParty == null)
        {
            Logger.Warning("Skipping late caravan reward for quest {QuestId}", caravan.StringId);
            return true;
        }
        caravan.OnQuestSucceeded();
        return true;
    }
}
