using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Tournaments.Messages;
using Common.Network;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

internal sealed class LadysKnightOutTournamentHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;
    private readonly ILadysKnightOutTournamentProgress progress;
    private readonly IOutcomeDispatcher dispatcher;
    private readonly IIssueOwnerResolver owners;
    private readonly LadysKnightOutFinalizationProofStrategy proof;

    public LadysKnightOutTournamentHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueResolver issues, IIssueOwnershipRegistry ownership, IPlayerManager players,
        ILadysKnightOutTournamentProgress progress,
        IOutcomeDispatcher dispatcher, IIssueOwnerResolver owners, LadysKnightOutFinalizationProofStrategy proof)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.issues = issues;
        this.ownership = ownership;
        this.players = players;
        this.progress = progress;
        this.dispatcher = dispatcher;
        this.owners = owners;
        this.proof = proof;
        broker.Subscribe<LadysKnightOutTournamentTriggered>(HandleNativeTriggered);
        broker.Subscribe<RequestLadysKnightOutTournamentOutcome>(HandleNativeOutcome);
        broker.Subscribe<TournamentSessionUpdated>(HandleUpdated);
        broker.Subscribe<TournamentSessionRemoved>(HandleRemoved);
    }

    public void Dispose()
    {
        broker.Unsubscribe<LadysKnightOutTournamentTriggered>(HandleNativeTriggered);
        broker.Unsubscribe<RequestLadysKnightOutTournamentOutcome>(HandleNativeOutcome);
        broker.Unsubscribe<TournamentSessionUpdated>(HandleUpdated);
        broker.Unsubscribe<TournamentSessionRemoved>(HandleRemoved);
    }

    private void HandleNativeTriggered(MessagePayload<LadysKnightOutTournamentTriggered> payload)
    {
        if (ModInformation.IsServer ||
            !objects.TryGetIdWithLogging(payload.What.Quest.QuestGiver, out var giverId))
            return;

        network.SendAll(new RequestLadysKnightOutTournamentOutcome(
            giverId,
            payload.What.Quest.QuestGiver.Issue.StringId,
            payload.What.Branch));
    }

    private void HandleNativeOutcome(MessagePayload<RequestLadysKnightOutTournamentOutcome> payload)
    {
        if (ModInformation.IsClient || payload.Who is not NetPeer peer || !players.TryGetPlayer(peer, out var player))
            return;

        GameThread.RunSafe(() =>
        {
            var data = payload.What;
            if (!issues.TryResolve(data.GiverId, data.IssueId, out _, out var issue, out _) ||
                issue.IssueQuest is not Quest quest || !quest.IsOngoing ||
                !ownership.TryGetOwner(data.GiverId, data.IssueId, out var owner) || owner != player.ControllerId ||
                data.Branch > (byte)LadysKnightOutBranch.TournamentFailure ||
                !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero))
                return;

            var branch = (LadysKnightOutBranch)data.Branch;
            progress.RecordOutcome(data.IssueId, branch);
            using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
                proof.TryRunBranch(quest, data.Branch);
            progress.RemoveQuest(data.IssueId);
        }, context: nameof(HandleNativeOutcome));
    }

    private void HandleUpdated(MessagePayload<TournamentSessionUpdated> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() => dispatcher.RunAsOwners<Quest>(quest =>
        {
            var issue = quest.QuestGiver.Issue;
            if (!owners.TryGetOwnerControllerId(issue, out var controllerId)) return;
            var issueId = issue.StringId;
            var outcome = progress.Observe(issueId, controllerId, payload.What.Snapshot, out _, out var round);
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
