using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using ProtoBuf;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts;

public enum ArtisanProductsLordAction : byte { Start, AcceptMerchant, RefuseMerchant, Journal, Rejected }

internal readonly struct ArtisanProductsLordRequested : IEvent
{
    public readonly Issue Issue;
    public readonly ArtisanProductsLordAction Action;
    public ArtisanProductsLordRequested(Issue issue, ArtisanProductsLordAction action) { Issue = issue; Action = action; }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestArtisanProductsLord : ICommand
{
    [ProtoMember(1)] public readonly string OwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly ArtisanProductsLordAction Action;
    [ProtoMember(4)] public readonly float Difficulty;
    public RequestArtisanProductsLord(string ownerId, string issueId, ArtisanProductsLordAction action, float difficulty)
    { OwnerId = ownerId; IssueId = issueId; Action = action; Difficulty = difficulty; }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanProductsLord : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string OwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly ArtisanProductsLordAction Action;
    [ProtoMember(5)] public readonly float Difficulty;
    [ProtoMember(6)] public readonly string[] Logs;
    public NetworkArtisanProductsLord(string ownerId, string issueId, string controllerId,
        ArtisanProductsLordAction action, float difficulty, string[] logs)
    { OwnerId = ownerId; IssueId = issueId; ControllerId = controllerId; Action = action; Difficulty = difficulty; Logs = logs; }
}

internal class ArtisanProductsLordHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver resolver;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IIssueAcceptPreconditions preconditions;
    private readonly IPlayerManager players;
    private readonly IControllerIdProvider controller;

    public ArtisanProductsLordHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueResolver resolver, IIssueOwnershipRegistry ownership, IIssueAcceptPreconditions preconditions,
        IPlayerManager players, IControllerIdProvider controller)
    {
        this.broker = broker; this.network = network; this.objects = objects; this.resolver = resolver;
        this.ownership = ownership; this.preconditions = preconditions; this.players = players; this.controller = controller;
        broker.Subscribe<ArtisanProductsLordRequested>(Request);
        broker.Subscribe<RequestArtisanProductsLord>(Receive);
        broker.Subscribe<NetworkArtisanProductsLord>(Apply);
    }

    public void Dispose()
    {
        broker.Unsubscribe<ArtisanProductsLordRequested>(Request);
        broker.Unsubscribe<RequestArtisanProductsLord>(Receive);
        broker.Unsubscribe<NetworkArtisanProductsLord>(Apply);
    }

    private void Request(MessagePayload<ArtisanProductsLordRequested> payload)
    {
        if (ModInformation.IsServer || !objects.TryGetIdWithLogging(payload.What.Issue.IssueOwner, out var ownerId)) return;
        network.SendAll(new RequestArtisanProductsLord(ownerId, payload.What.Issue.StringId, payload.What.Action,
            Campaign.Current.Models.IssueModel.GetIssueDifficultyMultiplier()));
    }

    internal void SendJournal(Issue issue)
    {
        if (!objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId) ||
            !ownership.TryGetOwner(ownerId, issue.StringId, out var owner)) return;
        network.SendAll(new NetworkArtisanProductsLord(ownerId, issue.StringId, owner, ArtisanProductsLordAction.Journal,
            issue._issueDifficultyMultiplier, issue.JournalEntries.Select(log => log.LogText.ToString()).ToArray()));
    }

    private void Receive(MessagePayload<RequestArtisanProductsLord> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer) || !players.TryGetPlayer(peer, out var player)) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!resolver.TryResolve(data.OwnerId, data.IssueId, out _, out var resolved, out _) || !(resolved is Issue issue) ||
                !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) || hero.PartyBelongedTo == null) return;
            using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
            {
                if (data.Action == ArtisanProductsLordAction.Start)
                {
                    if (hero.PartyBelongedTo.CurrentSettlement != issue.IssueOwner.CurrentSettlement) return;
                    // The decree costs and pays what the player's own dialogue quoted, the server's progress is not theirs
                    using (new IssueDifficultyOverride(data.Difficulty))
                    {
                        if (!issue.IsOngoingWithoutQuest || !preconditions.CanTake(issue, hero, player.ControllerId) ||
                            !issue.LordSolutionCondition(out _) || Clan.PlayerClan.Influence < issue.NeededInfluenceForLordSolution ||
                            !ownership.TrySetOwner(data.OwnerId, data.IssueId, player.ControllerId)) return;
                        issue.StartIssueWithLordSolution();
                    }
                }
                else
                {
                    if (!issue.IsSolvingWithLordSolution ||
                        !ownership.TryGetOwner(data.OwnerId, data.IssueId, out var owner) || owner != player.ControllerId) return;
                    if (data.Action == ArtisanProductsLordAction.AcceptMerchant) issue.CompleteIssueWithLordSolutionWithAcceptCounterOffer();
                    else if (data.Action == ArtisanProductsLordAction.RefuseMerchant)
                    {
                        if (!issue.LordSolutionCondition(out _) || Clan.PlayerClan.Influence < issue.NeededInfluenceForLordSolution)
                        {
                            issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject("The decree could not be issued because you no longer own the town or have enough influence. Speak to the merchant again to reconsider the offer or try again when eligible.")));
                            network.Send(peer, new NetworkArtisanProductsLord(data.OwnerId, data.IssueId, player.ControllerId,
                                ArtisanProductsLordAction.Rejected, issue._issueDifficultyMultiplier,
                                issue.JournalEntries.Select(log => log.LogText.ToString()).ToArray()));
                            return;
                        }
                        issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer();
                    }
                    else return;
                    ownership.Remove(data.OwnerId);
                }
                network.SendAll(new NetworkArtisanProductsLord(data.OwnerId, data.IssueId, player.ControllerId,
                    data.Action, issue._issueDifficultyMultiplier, issue.JournalEntries.Select(log => log.LogText.ToString()).ToArray()));
            }
        }, context: nameof(ArtisanProductsLordHandler));
    }

    private void Apply(MessagePayload<NetworkArtisanProductsLord> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!resolver.TryResolve(data.OwnerId, data.IssueId, out _, out var resolved, out _) || !(resolved is Issue issue)) return;
            var localOwner = data.ControllerId == controller.ControllerId;
            using (new AllowedThread())
            {
                if (data.Action == ArtisanProductsLordAction.Journal || data.Action == ArtisanProductsLordAction.Rejected)
                {
                    if (localOwner)
                        foreach (var text in data.Logs.Skip(issue.JournalEntries.Count))
                            issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject(text)));
                    return;
                }
                if (data.Action == ArtisanProductsLordAction.Start)
                {
                    ownership.TrySetOwner(data.OwnerId, data.IssueId, data.ControllerId);
                    if (localOwner) issue.StartIssueWithLordSolution();
                    else issue._issueState = IssueBase.IssueState.SolvingWithLordSolution;
                    issue._issueDifficultyMultiplier = data.Difficulty;
                }
                else
                {
                    if (localOwner)
                        foreach (var text in data.Logs.Skip(issue.JournalEntries.Count))
                            issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject(text)));
                    ownership.Remove(data.OwnerId);
                    issue.IssueFinalized();
                    // Ends the owner's journal entry, the issue rewards themselves only run on the server
                    CampaignEventDispatcher.Instance.OnIssueUpdated(issue, data.Action == ArtisanProductsLordAction.AcceptMerchant ?
                        IssueBase.IssueUpdateDetails.IssueFinishedWithBetrayal : IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess,
                        localOwner ? Hero.MainHero : null);
                }
            }
        }, context: nameof(ArtisanProductsLordHandler));
    }
}
