using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Tournaments.Messages;
using Quest = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

internal sealed class LadysKnightOutTournamentHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly ILadysKnightOutTournamentProgress progress;
    private readonly IOutcomeDispatcher dispatcher;
    private readonly IIssueOwnerResolver owners;
    private readonly LadysKnightOutFinalizationProofStrategy proof;

    public LadysKnightOutTournamentHandler(IMessageBroker broker, ILadysKnightOutTournamentProgress progress,
        IOutcomeDispatcher dispatcher, IIssueOwnerResolver owners, LadysKnightOutFinalizationProofStrategy proof)
    {
        this.broker = broker;
        this.progress = progress;
        this.dispatcher = dispatcher;
        this.owners = owners;
        this.proof = proof;
        broker.Subscribe<TournamentSessionUpdated>(HandleUpdated);
        broker.Subscribe<TournamentSessionRemoved>(HandleRemoved);
    }

    public void Dispose()
    {
        broker.Unsubscribe<TournamentSessionUpdated>(HandleUpdated);
        broker.Unsubscribe<TournamentSessionRemoved>(HandleRemoved);
    }

    private void HandleUpdated(MessagePayload<TournamentSessionUpdated> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() => dispatcher.RunAsOwners<Quest>(quest =>
        {
            var issue = quest.QuestGiver.Issue;
            if (!owners.TryGetOwnerControllerId(issue, out var controllerId)) return;
            var issueId = issue.StringId;
            var outcome = progress.Observe(issueId, controllerId, payload.What.Snapshot, out var round);
            if (outcome == LadysKnightOutTournamentOutcome.Ongoing) return;
            var branch = outcome == LadysKnightOutTournamentOutcome.Champion || quest.TournamentRoundGoal <= round + 1
                ? LadysKnightOutBranch.TournamentSuccess : LadysKnightOutBranch.TournamentFailure;
            progress.RecordOutcome(issueId, branch);
            proof.TryRunBranch(quest, (byte)branch);
            progress.RemoveQuest(issueId);
        }), context: nameof(LadysKnightOutTournamentHandler));
    }

    private void HandleRemoved(MessagePayload<TournamentSessionRemoved> payload)
    {
        if (ModInformation.IsServer)
            GameThread.RunSafe(() => progress.RemoveSession(payload.What.SessionId), context: nameof(LadysKnightOutTournamentHandler));
    }
}
