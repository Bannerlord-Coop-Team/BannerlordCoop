using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

namespace GameInterface.Services.Issues.Quests.CapturedByBountyHunters;

internal class CapturedByBountyHuntersFinalizationProofStrategy : IFinalizationProofStrategy
{
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest bountyHuntersQuest))
        {
            return false;
        }

        switch ((CapturedByBountyHuntersBranch)proof)
        {
            case CapturedByBountyHuntersBranch.SuccessByClearingHideout:
                bountyHuntersQuest.SuccessConsequences();
                return true;
            case CapturedByBountyHuntersBranch.FailByLosingHideoutBattle:
                bountyHuntersQuest.FailConsequences(isTimedOut: false);
                return true;
            default:
                return false;
        }
    }
}
