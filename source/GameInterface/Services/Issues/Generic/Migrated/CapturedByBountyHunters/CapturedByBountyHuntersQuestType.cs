using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Dispatch;
using GameInterface.Services.Issues.Messages;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.Migrated.CapturedByBountyHunters;

using Issue = CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue;
using Quest = CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

[ProtoContract(SkipConstructor = true)]
internal readonly struct BountyHuntersQuestAcceptFields
{
    [ProtoMember(1)]
    public readonly CampaignTime DueTime;
    [ProtoMember(2)]
    public readonly float Difficulty;
    [ProtoMember(3)]
    public readonly BountyHuntersJournalEntry[] Journal;

    public BountyHuntersQuestAcceptFields(CampaignTime dueTime, float difficulty, IEnumerable<JournalLog> journal)
    {
        DueTime = dueTime;
        Difficulty = difficulty;
        Journal = journal.Select(log => new BountyHuntersJournalEntry(log)).ToArray();
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct BountyHuntersAlternativeAcceptFields
{
    [ProtoMember(1)] public readonly AlternativeSolutionVanillaState State;
    [ProtoMember(2)] public readonly float Difficulty;
    [ProtoMember(3)] public readonly BountyHuntersJournalEntry[] Journal;

    public BountyHuntersAlternativeAcceptFields(AlternativeSolutionVanillaState state, float difficulty, IEnumerable<JournalLog> journal)
    {
        State = state;
        Difficulty = difficulty;
        Journal = journal.Select(log => new BountyHuntersJournalEntry(log)).ToArray();
    }
}

[QuestTypeModule]
internal static class CapturedByBountyHuntersQuestType
{
    private sealed class QuestAcceptStrategy : IRaceArbitratedAcceptMirrorStrategy<BountyHuntersQuestAcceptFields>
    {
        public void ReplayQuestAccepted(Hero owner)
        {
            if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
            if (!issue.CheckPreconditions(owner, out _)) return;

            using (new IssueDispatchReplayGuard())
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
                if (issue.IssueQuest is Quest quest && quest.JournalEntries.Count == 0)
                {
                    quest.QuestAcceptedConsequences();
                }
            }
        }

        public bool TryCaptureQuestFields(Hero owner, out BountyHuntersQuestAcceptFields fields)
        {
            fields = default;
            if (owner?.Issue?.IssueQuest is not Quest quest || !quest.IsOngoing || quest.JournalEntries.Count == 0) return false;
            fields = new BountyHuntersQuestAcceptFields(quest.QuestDueTime, owner.Issue.IssueDifficultyMultiplier, quest.JournalEntries);
            return true;
        }

        public void MirrorQuestAccepted(Hero owner, BountyHuntersQuestAcceptFields fields)
        {
            if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
            if (fields.Journal == null || fields.Journal.Length == 0)
                throw new InvalidOperationException("The bounty hunters acceptance has no journal");
            if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership))
                throw new InvalidOperationException("Issue ownership is unavailable while accepting bounty hunters quest");

            using (new AllowedThread())
            using (new IssueDispatchReplayGuard())
            using (new QuestSolutionStartAuthorityGuard())
            {
                if (ownership.IsLocalPeerOwner(owner))
                {
                    Campaign.Current.IssueManager.StartIssueQuest(owner);
                    var quest = (Quest)issue.IssueQuest;
                    quest.QuestDueTime = fields.DueTime;
                    quest.StartQuest();
                    foreach (var entry in fields.Journal) quest._journalEntries.Add(entry.ToJournalLog());
                    CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, false);
                }
                else
                {
                    issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                    issue.IsTriedToSolveBefore = true;
                    issue.IssueDueTime = CampaignTime.Never;
                }
                issue._issueDifficultyMultiplier = fields.Difficulty;
            }
        }

        public void RejectAcceptance(Hero owner) => AcceptMirrorSupport.RejectAcceptance(owner);
    }

    private sealed class AlternativeAcceptStrategy : IAlternativeAcceptMirrorStrategy<BountyHuntersAlternativeAcceptFields>
    {
        public void ReplayAlternativeAccepted(Hero owner)
        {
            using (new IssueDispatchReplayGuard())
            {
                owner.Issue.StartIssueWithAlternativeSolution();
            }
        }

        public bool TryCaptureAlternativeFields(Hero owner, out BountyHuntersAlternativeAcceptFields fields)
        {
            fields = default;
            if (owner?.Issue is not Issue issue || !issue.IsSolvingWithAlternative) return false;
            fields = new BountyHuntersAlternativeAcceptFields(AlternativeSolutionVanillaStateSync.Capture(issue),
                issue.IssueDifficultyMultiplier, issue.JournalEntries);
            return true;
        }

        public void MirrorAlternativeAccepted(Hero owner, BountyHuntersAlternativeAcceptFields fields)
        {
            if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
            if (fields.Journal == null || fields.Journal.Length == 0)
                throw new InvalidOperationException("The bounty hunters alternative acceptance has no journal");
            if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership))
                throw new InvalidOperationException("Issue ownership is unavailable while accepting bounty hunters alternative");

            using (new AllowedThread())
            {
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                issue.IsTriedToSolveBefore = true;
                issue._issueDifficultyMultiplier = fields.Difficulty;
                AlternativeSolutionVanillaStateSync.Apply(issue, fields.State, includeJournal: false);
                if (ownership.IsLocalPeerOwner(owner))
                {
                    foreach (var entry in fields.Journal) issue._journalEntries.Add(entry.ToJournalLog());
                    CampaignEventDispatcher.Instance.OnIssueLogAdded(issue, false);
                    CampaignEventDispatcher.Instance.OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest, Hero.MainHero);
                }
            }
        }

        public void RejectAcceptance(Hero owner) => AcceptMirrorSupport.RejectAcceptance(owner);
    }

    static CapturedByBountyHuntersQuestType()
    {
        QuestTypeRegistry.Register(QuestDescriptorBuilder.For<Issue, Quest>("CapturedByBountyHunters")
            .WithCreationTrigger(issue => MessageBroker.Instance.Publish(issue.IssueOwner, new CapturedByBountyHuntersIssueCreated(issue)))
            .WithQuestSolutionAccept(new QuestAcceptStrategy())
            .WithAlternativeAccept(new AlternativeAcceptStrategy())
            .Build());
    }
}
