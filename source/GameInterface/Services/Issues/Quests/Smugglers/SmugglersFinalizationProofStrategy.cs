using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Quest = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssueQuest;

namespace GameInterface.Services.Issues.Quests.Smugglers;

internal class SmugglersFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<SmugglersFinalizationProofStrategy>();

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest smugglersQuest))
        {
            return false;
        }

        switch ((SmugglersBranch)proof)
        {
            case SmugglersBranch.SuccessByPersuasion:
                smugglersQuest.SucceedQuest(smugglersQuest.QuestSuccessWithPersuasionLog);
                return true;
            case SmugglersBranch.SuccessByBribe:
                RunBribe(smugglersQuest);
                return true;
            default:
                return false;
        }
    }

    // SucceedQuestWithBribe without leaving the encounter, which only exists on the owner's client
    private static void RunBribe(Quest quest)
    {
        if (!quest.BribeCondition(out _))
        {
            Logger.Warning("The owner of {quest} cannot pay the bribe", quest.StringId);
            return;
        }

        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, quest.BribeAmount);
        quest.SucceedQuest(quest.QuestSuccessWithBribeLog);
    }
}
