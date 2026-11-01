using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using LiteNetLib;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;

namespace GameInterface.Services.Issues.Framework.Finalization;

public readonly struct AlternativeSolutionEndingObserved : IEvent
{
    public readonly IssueBase Issue;
    public readonly IssueBase.IssueUpdateDetails Ending;

    public AlternativeSolutionEndingObserved(IssueBase issue, IssueBase.IssueUpdateDetails ending)
    {
        Issue = issue;
        Ending = ending;
    }
}

/// <summary>Replays the owner's authoritative alternative acceptance and ending journals.</summary>
internal class AlternativeSolutionJournalHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver resolver;
    private readonly IIssueOwnerResolver owners;
    private readonly IQuestTypeRegistry registry;
    private readonly IControllerIdProvider controller;
    private readonly IPlayerManager players;
    private readonly ConditionalWeakTable<IssueBase, NetPeer> ownerPeers = new();
    private readonly object ownerPeersLock = new();

    public AlternativeSolutionJournalHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueResolver resolver, IIssueOwnerResolver owners, IQuestTypeRegistry registry,
        IControllerIdProvider controller, IPlayerManager players)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.resolver = resolver;
        this.owners = owners;
        this.registry = registry;
        this.controller = controller;
        this.players = players;
        broker.Subscribe<AlternativeSolutionEndingObserved>(HandleObserved);
        broker.Subscribe<AlternativeSolutionAcceptedOnServer>(RememberOwnerPeer);
        broker.Subscribe<NetworkAlternativeSolutionJournal>(HandleJournal);
    }

    public void Dispose()
    {
        broker.Unsubscribe<AlternativeSolutionEndingObserved>(HandleObserved);
        broker.Unsubscribe<AlternativeSolutionAcceptedOnServer>(RememberOwnerPeer);
        broker.Unsubscribe<NetworkAlternativeSolutionJournal>(HandleJournal);
    }

    private void RememberOwnerPeer(MessagePayload<AlternativeSolutionAcceptedOnServer> payload)
    {
        if (ModInformation.IsClient) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!resolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !players.TryGetPeer(data.ControllerId, out var peer)) return;

            lock (ownerPeersLock)
            {
                ownerPeers.Remove(issue);
                ownerPeers.Add(issue, peer);
            }
        }, context: nameof(AlternativeSolutionJournalHandler));
    }

    private void HandleObserved(MessagePayload<AlternativeSolutionEndingObserved> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (!IsEnding(payload.What.Ending) || !issue.IsSolvingWithAlternative ||
            !registry.IsRegistered(issue.GetType())) return;
        if (!objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        var report = issue.JournalEntries.LastOrDefault()?.LogText;
        // Vanilla failure binds the companion globally instead of saving it on the report.
        if (payload.What.Ending == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest &&
            report != null && issue.AlternativeSolutionHero != null)
            StringHelpers.SetCharacterProperties("COMPANION", issue.AlternativeSolutionHero.CharacterObject, report);
        if (!TryGetOwnerPeer(issue, out var peer)) return;

        var entries = issue.JournalEntries.Select(log => new IssueJournalEntry(log)).ToArray();
        network.Send(peer, new NetworkAlternativeSolutionJournal(giverId, issue.StringId, payload.What.Ending, entries));
    }

    private bool TryGetOwnerPeer(IssueBase issue, out NetPeer peer)
    {
        if (owners.TryGetOwnerControllerId(issue, out var owner) && players.TryGetPeer(owner, out peer))
            return peer.ConnectionState == ConnectionState.Connected;

        lock (ownerPeersLock)
            return ownerPeers.TryGetValue(issue, out peer) && peer.ConnectionState == ConnectionState.Connected;
    }

    private void HandleJournal(MessagePayload<NetworkAlternativeSolutionJournal> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        var isAcceptance = data.Ending == IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest;
        GameThread.RunSafe(() =>
        {
            if ((!isAcceptance && !IsEnding(data.Ending)) || !resolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !issue.IsSolvingWithAlternative || !owners.TryGetOwnerControllerId(issue, out var owner) ||
                owner != controller.ControllerId) return;

            using (new AllowedThread())
            {
                issue._journalEntries.Clear();
                foreach (var entry in data.Entries ?? Array.Empty<IssueJournalEntry>())
                    issue._journalEntries.Add(entry.ToJournalLog());
                if (isAcceptance)
                {
                    var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog(
                        (JournalLogEntry entry) => entry.IsRelatedTo(issue));
                    archive?.Update(issue.JournalEntries, data.Ending);
                }
                else
                    CampaignEventDispatcher.Instance.OnIssueUpdated(issue, data.Ending,
                        data.Ending == IssueBase.IssueUpdateDetails.IssueCancel && !issue.IsTriedToSolveBefore
                            ? null
                            : Hero.MainHero);
            }
        }, context: nameof(AlternativeSolutionJournalHandler));
    }

    private static bool IsEnding(IssueBase.IssueUpdateDetails ending) =>
        ending == IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest ||
        ending == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest ||
        ending == IssueBase.IssueUpdateDetails.IssueCancel;
}
