using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;

internal enum HeadmanHerdBranch : byte
{
    Deliver = 1,
    Keep = 2,
}

internal class HeadmanNeedsToDeliverAHerdFinalizationProofStrategy : IFinalizationProofStrategy
{
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest herd)) return false;
        if (!herd.IsOngoing || MobileParty.MainParty.CurrentSettlement != herd._targetSettlement) return true;

        switch ((HeadmanHerdBranch)proof)
        {
            case HeadmanHerdBranch.Deliver:
                if (herd.GetAvailableRequestedItemCountOnPlayer(herd._herdTypeToDeliver) >= herd._animalCountToDeliver)
                    herd.DeliverHerdOnConsequence();
                return true;
            case HeadmanHerdBranch.Keep:
                herd.DeliverHerdRejectOnConsequence();
                return true;
            default:
                return false;
        }
    }
}
