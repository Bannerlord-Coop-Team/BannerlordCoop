using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Issue = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssue;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

internal sealed class LadysKnightOutLordHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IIssueAcceptPreconditions preconditions;
    private readonly IPlayerManager players;
    private readonly IControllerIdProvider controller;
    private readonly IOutcomeDispatcher dispatcher;

    public LadysKnightOutLordHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueResolver issues, IIssueOwnershipRegistry ownership, IIssueAcceptPreconditions preconditions,
        IPlayerManager players, IControllerIdProvider controller, IOutcomeDispatcher dispatcher)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.issues = issues;
        this.ownership = ownership;
        this.preconditions = preconditions;
        this.players = players;
        this.controller = controller;
        this.dispatcher = dispatcher;
        broker.Subscribe<LadysKnightOutLordTriggered>(HandleTriggered);
        broker.Subscribe<RequestLadysKnightOutLord>(HandleRequest);
        broker.Subscribe<NetworkLadysKnightOutLordAccepted>(HandleAccepted);
    }

    public void Dispose()
    {
        broker.Unsubscribe<LadysKnightOutLordTriggered>(HandleTriggered);
        broker.Unsubscribe<RequestLadysKnightOutLord>(HandleRequest);
        broker.Unsubscribe<NetworkLadysKnightOutLordAccepted>(HandleAccepted);
    }

    private void HandleTriggered(MessagePayload<LadysKnightOutLordTriggered> payload)
    {
        if (ModInformation.IsServer || !objects.TryGetIdWithLogging(payload.What.Issue.IssueOwner, out var giverId)) return;
        network.SendAll(new RequestLadysKnightOutLord(
            giverId,
            payload.What.Issue.StringId,
            payload.What.Complete,
            payload.What.Issue.IssueDifficultyMultiplier));
    }

    private void HandleRequest(MessagePayload<RequestLadysKnightOutLord> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer) || !players.TryGetPlayer(peer, out var player)) return;
        GameThread.RunSafe(() =>
        {
            var data = payload.What;
            if (!issues.TryResolve(data.GiverId, data.IssueId, out _, out var resolved, out _) || !(resolved is Issue issue) ||
                !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return;

            using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
            using (new IssueDifficultyOverride(data.Difficulty))
            {
                if (data.Complete)
                {
                    if (!issue.IsSolvingWithLordSolution || !ownership.TryGetOwner(data.GiverId, data.IssueId, out var owner) ||
                        owner != player.ControllerId) return;
                    issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer();
                    dispatcher.After(issue, IssueOutcome.QuestSuccess);
                    return;
                }

                if (!issue.IsOngoingWithoutQuest || !preconditions.CanTake(issue, hero, player.ControllerId) ||
                    issue.IssueOwner.CurrentSettlement?.OwnerClan != hero.Clan ||
                    hero.Clan.Influence < issue.NeededInfluenceForLordSolution || !issue.LordSolutionCondition(out _) ||
                    !ownership.TrySetOwner(data.GiverId, data.IssueId, player.ControllerId)) return;
                issue.StartIssueWithLordSolution();
                // Only the owner's client opens the menu that completes this solution.
                CampaignEvents.BeforeGameMenuOpenedEvent.ClearListeners(issue);
                network.SendAll(new NetworkLadysKnightOutLordAccepted(
                    data.GiverId,
                    data.IssueId,
                    player.ControllerId,
                    issue.IssueDifficultyMultiplier,
                    issue.JournalEntries[0].LogText.ToString()));
            }
        }, context: nameof(LadysKnightOutLordHandler));
    }

    private void HandleAccepted(MessagePayload<NetworkLadysKnightOutLordAccepted> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() =>
        {
            var data = payload.What;
            if (!issues.TryResolve(data.GiverId, data.IssueId, out _, out var issue, out _) || !(issue is Issue) || issue.IsSolvingWithLordSolution) return;
            var strayQuest = issue.IssueQuest;
            if (strayQuest != null)
            {
                issue.IssueQuest = null;
                using (new AllowedThread()) strayQuest.CompleteQuestWithCancel();
            }
            ownership.TrySetOwner(data.GiverId, data.IssueId, data.ControllerId);
            issue._issueDifficultyMultiplier = data.Difficulty;
            issue._issueState = IssueBase.IssueState.SolvingWithLordSolution;
            issue.IsTriedToSolveBefore = data.ControllerId == controller.ControllerId;
            if (data.ControllerId != controller.ControllerId) return;
            using (new AllowedThread()) issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject(data.StartLog)));
            CampaignEvents.BeforeGameMenuOpenedEvent.AddNonSerializedListener(issue, issue.BeforeGameMenuOpened);
            if (!Campaign.Current.ConversationManager.IsConversationInProgress) issue.BeforeGameMenuOpened(null);
        }, context: nameof(LadysKnightOutLordHandler));
    }
}
