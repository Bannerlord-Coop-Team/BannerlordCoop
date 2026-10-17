using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal enum CaravanAmbushBranch : byte
{
    CollectReward = 1,
}

internal class CaravanAmbushFinalizationProofStrategy : IFinalizationProofStrategy
{
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest caravan) || proof != (byte)CaravanAmbushBranch.CollectReward) return false;
        if (!caravan.IsOngoing || !caravan._isCaravanSaved || caravan._caravanParty == null) return true;
        var radius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius;
        if (MobileParty.MainParty.Position.DistanceSquared(caravan._caravanParty.Position) > radius * radius) return true;
        caravan.OnQuestSucceeded();
        return true;
    }
}
