using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordsNeedsTutor;

internal sealed class LordsNeedsTutorFinalizationProofStrategy : IFinalizationProofStrategy
{
    private readonly ITutorJournal journal;
    public LordsNeedsTutorFinalizationProofStrategy(ITutorJournal journal) => this.journal = journal;

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (quest is not Quest tutor || !tutor.IsOngoing || tutor._startQuestLog == null) return false;
        var pupil = tutor._youngHero;
        if (Hero.MainHero.IsPrisoner || !pupil.IsAlive ||
            (pupil.PartyBelongedTo != MobileParty.MainParty &&
                (MobileParty.MainParty.CurrentSettlement == null || pupil.CurrentSettlement != MobileParty.MainParty.CurrentSettlement))) return true;
        switch ((LordsNeedsTutorBranch)proof)
        {
            case LordsNeedsTutorBranch.Success:
                if (tutor._firstConversationInitialized && pupil.HeroDeveloper.GetTotalSkillPoints() >= tutor._targetSkillGain)
                    tutor.QuestCompletedWithSuccessAfterConversation();
                return true;
            case LordsNeedsTutorBranch.Introduction:
                tutor.FirstConversationEndConsequence();
                journal.Send(tutor);
                return true;
            case LordsNeedsTutorBranch.Rejoin:
                if (tutor._firstConversationInitialized && !tutor._questCompletedStartConversation &&
                    pupil.CompanionOf == Hero.MainHero.Clan && tutor.PupilJoinMeCondition())
                    MobileParty.MainParty.MemberRoster.AddToCounts(pupil.CharacterObject, 1);
                return true;
            default: return false;
        }
    }
}
