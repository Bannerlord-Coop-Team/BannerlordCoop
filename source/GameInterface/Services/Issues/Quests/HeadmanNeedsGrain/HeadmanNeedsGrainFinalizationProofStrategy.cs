using Common.Logging;
using Serilog;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;

internal enum HeadmanNeedsGrainBranch : byte
{
    DeliverGrain = 1,
}

internal class HeadmanNeedsGrainFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeadmanNeedsGrainFinalizationProofStrategy>();

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest grainQuest) || proof != (byte)HeadmanNeedsGrainBranch.DeliverGrain) return false;

        var party = Hero.MainHero?.PartyBelongedTo;
        var village = quest.QuestGiver.CurrentSettlement;
        if (party == null || village == null || party.CurrentSettlement != village ||
            party.ItemRoster.GetItemNumber(DefaultItems.Grain) < grainQuest._neededGrainAmount)
        {
            Logger.Warning("The owner of {giver}'s quest is not in the village or does not carry the requested grain", quest.QuestGiver.StringId);
            return true;
        }

        grainQuest.Success();
        return true;
    }
}
