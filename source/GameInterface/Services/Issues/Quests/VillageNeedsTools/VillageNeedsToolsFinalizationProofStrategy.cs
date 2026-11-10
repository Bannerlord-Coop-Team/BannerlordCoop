using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest;

namespace GameInterface.Services.Issues.Quests.VillageNeedsTools;

internal enum VillageNeedsToolsBranch : byte
{
    DeliverTools = 1,
}

/// <summary>Checks the owner's location and inventory before running the real hand-in.</summary>
internal class VillageNeedsToolsFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillageNeedsToolsFinalizationProofStrategy>();

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest toolsQuest) || (VillageNeedsToolsBranch)proof != VillageNeedsToolsBranch.DeliverTools)
        {
            return false;
        }

        if (MobileParty.MainParty.CurrentSettlement != toolsQuest.QuestGiver.CurrentSettlement || !toolsQuest.PlayerHasTools())
        {
            Logger.Warning("The owner of {quest} must bring the requested tools to the giver's village", quest.StringId);
            return true;
        }

        toolsQuest.FinishQuestSuccess1();
        return true;
    }
}
