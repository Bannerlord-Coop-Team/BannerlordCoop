using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArmyOfPoachers;

internal enum ArmyOfPoachersBranch : byte
{
    Agreement = 1,
    LeavePoachersAlone = 2,
}

internal class ArmyOfPoachersFinalizationProofStrategy : IFinalizationProofStrategy
{
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest poachers) || !poachers.IsOngoing)
        {
            return false;
        }

        // A late dialogue callback must not pay after the owner left or started fighting.
        if (poachers._poachersParty?.IsActive != true || poachers._poachersParty.MapEvent != null ||
            Hero.MainHero.PartyBelongedTo?.CurrentSettlement != poachers._questVillage.Settlement)
        {
            return true;
        }

        switch ((ArmyOfPoachersBranch)proof)
        {
            case ArmyOfPoachersBranch.Agreement:
                poachers.QuestSuccessPlayerComesToAnAgreementWithPoachers();
                return true;
            case ArmyOfPoachersBranch.LeavePoachersAlone:
                poachers.QuestFailedAfterTalkingWithPoachers();
                return true;
            default:
                return false;
        }
    }
}
