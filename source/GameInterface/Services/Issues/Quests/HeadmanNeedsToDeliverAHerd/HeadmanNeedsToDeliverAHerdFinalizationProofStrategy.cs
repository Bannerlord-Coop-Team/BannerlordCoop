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
    private static readonly Serilog.ILogger Logger = Common.Logging.LogManager.GetLogger<HeadmanNeedsToDeliverAHerdFinalizationProofStrategy>();
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest herd)) return false;
        if (!herd.IsOngoing || MobileParty.MainParty.CurrentSettlement != herd._targetSettlement) return true;

        switch ((HeadmanHerdBranch)proof)
        {
            case HeadmanHerdBranch.Deliver:
                if (herd.GetAvailableRequestedItemCountOnPlayer(herd._herdTypeToDeliver) >= herd._animalCountToDeliver)
                    herd.DeliverHerdOnConsequence();
                else
                    Logger.Warning("The owner of {quest} does not carry the requested herd for branch {proof}", herd.StringId, proof);
                return true;
            case HeadmanHerdBranch.Keep:
                herd.DeliverHerdRejectOnConsequence();
                return true;
            default:
                return false;
        }
    }
}
