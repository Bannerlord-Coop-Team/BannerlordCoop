using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Dispatch;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using ProtoBuf;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.Migrated.ArtisanOverpricedGoods;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;
using Quest = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ArtisanOverpricedGoodsQuestAccepted
{
    [ProtoMember(1)]
    public readonly ArtisanOverpricedGoodsIssueValues Values;
    [ProtoMember(2)]
    public readonly CampaignTime QuestDueTime;

    public ArtisanOverpricedGoodsQuestAccepted(ArtisanOverpricedGoodsIssueValues values, CampaignTime questDueTime)
    {
        Values = values;
        QuestDueTime = questDueTime;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ArtisanOverpricedGoodsAlternativeAccepted
{
    [ProtoMember(1)]
    public readonly ArtisanOverpricedGoodsIssueValues Values;
    [ProtoMember(2)]
    public readonly AlternativeSolutionVanillaState State;

    public ArtisanOverpricedGoodsAlternativeAccepted(ArtisanOverpricedGoodsIssueValues values,
        AlternativeSolutionVanillaState state)
    {
        Values = values;
        State = state;
    }
}

[QuestTypeModule]
internal static class ArtisanOverpricedGoodsQuestType
{
    private sealed class QuestAcceptMirror : IRaceArbitratedAcceptMirrorStrategy<ArtisanOverpricedGoodsQuestAccepted>
    {
        public void ReplayQuestAccepted(Hero owner)
        {
            if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
            if (!issue.CheckPreconditions(owner, out _)) return;
            using (new IssueDispatchReplayGuard())
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
                if (issue.IssueQuest is Quest quest && quest._playerStartsQuestLog == null)
                    quest.QuestAcceptedConsequences();
            }
        }

        public bool TryCaptureQuestFields(Hero owner, out ArtisanOverpricedGoodsQuestAccepted fields)
        {
            fields = default;
            if (owner?.Issue is not Issue issue || issue.IssueQuest is not Quest quest || !quest.IsOngoing) return false;
            if (!ContainerProvider.TryResolve<IArtisanOverpricedGoodsIssueInterface>(out var issueInterface)) return false;
            fields = new(issueInterface.CaptureValues(issue), quest.QuestDueTime);
            return true;
        }

        public void MirrorQuestAccepted(Hero owner, ArtisanOverpricedGoodsQuestAccepted fields)
        {
            if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
            if (!ContainerProvider.TryResolve<IArtisanOverpricedGoodsIssueInterface>(out var issueInterface)) return;
            if (!ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context) || !context.TryEnter(owner, out var scope))
                throw new InvalidOperationException("Cannot resolve the accepted Artisan quest owner.");

            using (scope)
            using (new AllowedThread())
            using (new IssueDispatchReplayGuard())
            using (new QuestSolutionStartAuthorityGuard())
            {
                issueInterface.ApplyValues(issue, fields.Values);
                Campaign.Current.IssueManager.StartIssueQuest(owner);
                if (issue.IssueQuest is not Quest quest) return;
                issueInterface.ApplyValues(issue, fields.Values);
                quest.QuestDueTime = fields.QuestDueTime;
                if (quest._playerStartsQuestLog == null) quest.QuestAcceptedConsequences();
            }
        }

        public void RejectAcceptance(Hero owner)
        {
            // Acceptance creates the quest only after the server agrees.
        }
    }

    private sealed class AlternativeAcceptMirror : IAlternativeAcceptMirrorStrategy<ArtisanOverpricedGoodsAlternativeAccepted>, IAlternativeAcceptPreparation
    {
        public void PrepareForMirror(Hero owner)
        {
            if (ContainerProvider.TryResolve<IArtisanAlternativeSelection>(out var selection)) selection.Restore(owner);
        }

        public void ReplayAlternativeAccepted(Hero owner)
        {
            // The shared runner has already charged funding and started the expedition.
        }

        public bool TryCaptureAlternativeFields(Hero owner, out ArtisanOverpricedGoodsAlternativeAccepted fields)
        {
            fields = default;
            if (owner?.Issue is not Issue issue || !issue.IsSolvingWithAlternative) return false;
            if (!ContainerProvider.TryResolve<IArtisanOverpricedGoodsIssueInterface>(out var issueInterface)) return false;
            fields = new(issueInterface.CaptureValues(issue), AlternativeSolutionVanillaStateSync.Capture(issue));
            return true;
        }

        public void MirrorAlternativeAccepted(Hero owner, ArtisanOverpricedGoodsAlternativeAccepted fields)
        {
            if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
            if (!ContainerProvider.TryResolve<IArtisanOverpricedGoodsIssueInterface>(out var issueInterface)) return;
            if (!ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context) || !context.TryEnter(owner, out var scope))
                throw new InvalidOperationException("Cannot resolve the accepted Artisan expedition owner.");
            using (scope)
            using (new AllowedThread())
            {
                issueInterface.ApplyValues(issue, fields.Values);
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                issue.IsTriedToSolveBefore = true;
                AlternativeSolutionVanillaStateSync.Apply(issue, fields.State);
            }
        }

        public void RejectAcceptance(Hero owner)
        {
            if (ContainerProvider.TryResolve<IArtisanAlternativeSelection>(out var selection)) selection.Finish(owner);
        }
    }

    static ArtisanOverpricedGoodsQuestType()
    {
        QuestTypeRegistry.Register(QuestDescriptorBuilder.For<Issue, Quest>("ArtisanOverpricedGoods")
            .WithCreationTrigger(issue => MessageBroker.Instance.Publish(issue.IssueOwner, new ArtisanOverpricedGoodsIssueCreated(issue)))
            .WithQuestSolutionAccept(new QuestAcceptMirror())
            .WithAlternativeAccept(new AlternativeAcceptMirror())
            .WithAlternativeAcceptTrigger((owner, controller) =>
            {
                if (ContainerProvider.TryResolve<IArtisanAlternativeSelection>(out var selection)) selection.Restore(owner);
            })
            .WithQuestSuccessValidation((issue, party) => false)
            .WithQuestFailValidation(issue => false)
            .WithQuestBetrayalValidation(issue => false)
            .Build());
    }
}
