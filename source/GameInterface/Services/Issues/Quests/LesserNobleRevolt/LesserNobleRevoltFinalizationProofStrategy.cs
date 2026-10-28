using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

internal sealed class LesserNobleRevoltFinalizationProofStrategy : IFinalizationProofStrategy
{
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest revolt))
        {
            return false;
        }

        switch ((LesserNobleRevoltBranch)proof)
        {
            case LesserNobleRevoltBranch.SuccessByAgreement:
                revolt.QuestSuccessPlayerComesToAnAgreementWithLesserNobleParty();
                return true;
            case LesserNobleRevoltBranch.SuccessByBattle:
                revolt.QuestSuccessWithPlayerDefeatedLesserNobleParty();
                return true;
            case LesserNobleRevoltBranch.FailureByBattle:
                revolt.QuestFailWithPlayerDefeatedAgainstLesserNobleParty();
                return true;
            case LesserNobleRevoltBranch.FailureByRelease:
                revolt.QuestFailedAfterTalkingWithLesserNobleParty();
                return true;
            default:
                return false;
        }
    }
}
