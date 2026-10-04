using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Dispatch;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;
using Quest = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

public interface IHeadmanNeedsToDeliverAHerdAcceptInterface :
    IRaceArbitratedAcceptMirrorStrategy<HeadmanHerdQuestAcceptFields>,
    IAlternativeAcceptMirrorStrategy<HeadmanHerdAlternativeAcceptFields>
{
    new void RejectAcceptance(Hero owner);
}

public sealed class HeadmanNeedsToDeliverAHerdAcceptInterface : IHeadmanNeedsToDeliverAHerdAcceptInterface
{
    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;

        if (!issue.CheckPreconditions(owner, out _)) return;
        using (new IssueDispatchReplayGuard())
        {
            if (!Campaign.Current.IssueManager.StartIssueQuest(owner)) return;
            if (issue.IssueQuest is Quest quest && quest._playerStartsQuestLog == null)
            {
                quest.QuestAcceptedConsequences();
            }
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out HeadmanHerdQuestAcceptFields fields)
    {
        fields = default;
        if (owner?.Issue is not Issue issue || issue.IssueQuest is not Quest quest) return false;

        fields = new HeadmanHerdQuestAcceptFields(
            quest.StringId, quest._animalCountToDeliver, quest._rewardGold,
            quest.QuestDueTime, issue._issueDifficultyMultiplier, new HeadmanHerdJournalEntryData(quest._playerStartsQuestLog));
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, HeadmanHerdQuestAcceptFields fields)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            issue._issueDifficultyMultiplier = fields.Difficulty;
            var quest = new Quest(fields.QuestId, owner, fields.DueTime,
                fields.AnimalCount, issue._herdTypeToDeliver, issue._targetSettlement,
                fields.RewardGold, issue._targetHero);
            issue.IssueQuest = quest;
            issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
            issue.IsTriedToSolveBefore = true;
            issue.IssueDueTime = CampaignTime.Never;

            // Resource changes arrive separately from the authoritative acceptance.
            quest.StartQuest();
            quest._playerStartsQuestLog = fields.StartLog.ToLog();
            quest._journalEntries.Add(quest._playerStartsQuestLog);
            CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, false);
            quest.AddTrackedObject(issue._targetSettlement);
            quest.AddTrackedObject(issue._targetHero);
            Campaign.Current.ConversationManager.AddDialogFlow(quest.GetDeliveryDialogFlow(), quest);
        }
    }

    public void ReplayAlternativeAccepted(Hero owner)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        using (new IssueDispatchReplayGuard())
        {
            issue.StartIssueWithAlternativeSolution();
        }
    }

    public bool TryCaptureAlternativeFields(Hero owner, out HeadmanHerdAlternativeAcceptFields fields)
    {
        fields = default;
        if (owner?.Issue is not Issue issue || !issue.IsSolvingWithAlternative) return false;
        fields = new HeadmanHerdAlternativeAcceptFields(issue._issueDifficultyMultiplier,
            AlternativeSolutionVanillaStateSync.Capture(issue), new HeadmanHerdJournalEntryData(issue.JournalEntries[0]));
        return true;
    }

    public void MirrorAlternativeAccepted(Hero owner, HeadmanHerdAlternativeAcceptFields fields)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        if (ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership))
            ownership.RecordCurrentOwner(issue);
        using (new AllowedThread())
        {
            issue._issueDifficultyMultiplier = fields.Difficulty;
            issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
            issue.IsTriedToSolveBefore = true;
            AlternativeSolutionVanillaStateSync.Apply(issue, fields.State, fields.StartLog.ToLog());
            CampaignEventDispatcher.Instance.OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest, Hero.MainHero);
        }
    }

    public void RejectAcceptance(Hero owner)
    {
        // Herd acceptance is not speculative; a losing response must preserve the winning player's quest.
        if (owner?.Issue is Issue { IsOngoingWithoutQuest: true } issue)
        {
            using (new AllowedThread()) issue.AlternativeSolutionSentTroops.Clear();
            if (ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership))
                ownership.ForgetUnacceptedIssue(issue);
        }
    }

}

[ProtoContract(SkipConstructor = true)]
public readonly struct HeadmanHerdQuestAcceptFields
{
    [ProtoMember(1)]
    public readonly string QuestId;
    [ProtoMember(2)]
    public readonly int AnimalCount;
    [ProtoMember(3)]
    public readonly int RewardGold;
    [ProtoMember(4)]
    public readonly CampaignTime DueTime;
    [ProtoMember(5)]
    public readonly float Difficulty;
    [ProtoMember(6)]
    public readonly HeadmanHerdJournalEntryData StartLog;

    public HeadmanHerdQuestAcceptFields(string questId, int animalCount, int rewardGold,
        CampaignTime dueTime, float difficulty, HeadmanHerdJournalEntryData startLog)
    {
        QuestId = questId;
        AnimalCount = animalCount;
        RewardGold = rewardGold;
        DueTime = dueTime;
        Difficulty = difficulty;
        StartLog = startLog;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct HeadmanHerdAlternativeAcceptFields
{
    [ProtoMember(1)]
    public readonly float Difficulty;

    [ProtoMember(2)]
    public readonly AlternativeSolutionVanillaState State;
    [ProtoMember(3)]
    public readonly HeadmanHerdJournalEntryData StartLog;

    public HeadmanHerdAlternativeAcceptFields(float difficulty, AlternativeSolutionVanillaState state, HeadmanHerdJournalEntryData startLog)
    {
        Difficulty = difficulty;
        State = state;
        StartLog = startLog;
    }
}
