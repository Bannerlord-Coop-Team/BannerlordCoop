using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Players;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Conversation;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Framework.Conversation;

internal sealed class PersuasionSessionService : IPersuasionSessionService, IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PersuasionSessionService>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager playerManager;
    private readonly ConversationPartyTracker partyTracker;
    private readonly Dictionary<SessionKey, (string Target, NetPeer Peer)> sessions = new();

    public PersuasionSessionService(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager,
        ConversationPartyTracker partyTracker)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.ownership = ownership;
        this.playerManager = playerManager;
        this.partyTracker = partyTracker;

        messageBroker.Subscribe<PersuasionStarted>(HandleStarted);
        messageBroker.Subscribe<NetworkConversationEnded>(HandleConversationEnded);
        messageBroker.Subscribe<IssueOutcomeObserved>(HandleOutcomeObserved);
        messageBroker.Subscribe<PlayerDisconnected>(HandlePlayerRemoved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PersuasionStarted>(HandleStarted);
        messageBroker.Unsubscribe<NetworkConversationEnded>(HandleConversationEnded);
        messageBroker.Unsubscribe<IssueOutcomeObserved>(HandleOutcomeObserved);
        messageBroker.Unsubscribe<PlayerDisconnected>(HandlePlayerRemoved);
    }

    public void Start(QuestBase quest, object target)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (ModInformation.IsServer || issue == null ||
            !objectManager.TryGetIdWithLogging(target, out var targetId) ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId))
        {
            return;
        }

        network.SendAll(new PersuasionStarted(ownerId, issue.StringId, quest.StringId, targetId));
    }

    public bool TryConsume(QuestBase quest, object target)
    {
        if (ModInformation.IsClient)
        {
            return true;
        }

        var issue = IssueManager.GetIssueOfQuest(quest);
        string ownerId;
        string heroId;
        if (issue == null || !objectManager.TryGetId(issue.IssueOwner, out ownerId) ||
            !objectManager.TryGetId(Hero.MainHero, out heroId) ||
            !playerManager.Players.Any(player => player.HeroId == heroId))
        {
            return false;
        }

        var key = new SessionKey(ownerId, issue.StringId, quest.StringId, heroId);
        if (!sessions.TryGetValue(key, out var sessionTarget) ||
            !objectManager.TryGetId(target, out var targetId) || sessionTarget.Target != targetId ||
            !sessions.Remove(key))
        {
            Logger.Warning("Rejected persuasion success for {issue}: no live session", issue.StringId);
            return false;
        }

        return true;
    }

    private void HandleStarted(MessagePayload<PersuasionStarted> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player))
        {
            return;
        }

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            IssueBase issue = null;
            string owner = null;
            Hero hero = null;
            MobileParty party = null;
            MobileParty targetParty = null;
            if (!TryResolveIssue(data.IssueOwnerId, data.IssueId, out issue) ||
                !issue.IsSolvingWithQuest ||
                issue.IssueQuest == null || !issue.IssueQuest.IsOngoing || data.Generation != issue.IssueQuest.StringId ||
                !ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out owner) || owner != player.ControllerId ||
                !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out hero) ||
                !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out party) ||
                !TryGetTargetParty(data.TargetId, out targetParty) ||
                !AreTogether(party, targetParty))
            {
                Logger.Warning("Rejected persuasion start for {issue}", data.IssueId);
                return;
            }

            var key = new SessionKey(data.IssueOwnerId, data.IssueId, data.Generation, player.HeroId);
            if (sessions.ContainsKey(key))
            {
                Logger.Warning("Rejected duplicate persuasion start for {issue}", data.IssueId);
                return;
            }

            sessions.Add(key, (data.TargetId, peer));
        }, context: nameof(PersuasionSessionService));
    }

    private void HandleConversationEnded(MessagePayload<NetworkConversationEnded> payload)
    {
        if (payload.Who is NetPeer peer)
        {
            GameThread.RunSafe(() => ClearPeer(peer), context: nameof(PersuasionSessionService));
        }
    }

    private void HandlePlayerRemoved(MessagePayload<PlayerDisconnected> payload)
    {
        var peer = payload.What.PlayerId;
        GameThread.RunSafe(() => ClearPeer(peer), context: nameof(PersuasionSessionService));
    }

    private void ClearPeer(NetPeer peer)
    {
        foreach (var key in new List<SessionKey>(sessions.Keys))
        {
            if (sessions[key].Peer == peer) sessions.Remove(key);
        }
    }
    private void HandleOutcomeObserved(MessagePayload<IssueOutcomeObserved> payload)
    {
        var issue = payload.What.Issue;
        GameThread.RunSafe(() =>
        {
            if (objectManager.TryGetId(issue.IssueOwner, out var ownerId)) ClearIssue(ownerId, issue.StringId);
        }, context: nameof(PersuasionSessionService));
    }

    private void ClearIssue(string ownerId, string issueId)
    {
        foreach (var key in new List<SessionKey>(sessions.Keys))
            if (key.IssueOwnerId == ownerId && key.IssueId == issueId) sessions.Remove(key);
    }

    private bool TryGetTargetParty(string targetId, out MobileParty party)
    {
        if (objectManager.TryGetObject<MobileParty>(targetId, out party))
        {
            return true;
        }

        if (objectManager.TryGetObject<Hero>(targetId, out var hero))
        {
            party = hero.PartyBelongedTo;
            return party != null;
        }

        party = null;
        return false;
    }

    private bool TryResolveIssue(string issueOwnerId, string issueId, out IssueBase issue)
    {
        issue = null;
        return objectManager.TryGetObject<Hero>(issueOwnerId, out var owner) &&
            (issue = owner.Issue) != null && issue.StringId == issueId;
    }

    private bool AreTogether(MobileParty owner, MobileParty target)
    {
        if (owner.CurrentSettlement != null && owner.CurrentSettlement == target.CurrentSettlement) return true;
        return owner.Party != null && target.Party != null &&
            objectManager.TryGetId(owner.Party, out var ownerId) &&
            objectManager.TryGetId(target.Party, out var targetId) &&
            partyTracker.IsEngagerParty(targetId, ownerId);
    }

    private readonly struct SessionKey : IEquatable<SessionKey>
    {
        public SessionKey(string issueOwnerId, string issueId, string generation, string ownerHeroId)
        {
            IssueOwnerId = issueOwnerId;
            IssueId = issueId;
            Generation = generation;
            OwnerHeroId = ownerHeroId;
        }

        public string IssueOwnerId { get; }
        public string IssueId { get; }
        public string Generation { get; }
        public string OwnerHeroId { get; }

        public bool Equals(SessionKey other) =>
            IssueOwnerId == other.IssueOwnerId && IssueId == other.IssueId &&
            Generation == other.Generation && OwnerHeroId == other.OwnerHeroId;

        public override bool Equals(object obj) => obj is SessionKey other && Equals(other);
        public override int GetHashCode() => ((((IssueOwnerId?.GetHashCode() ?? 0) * 397) ^ (IssueId?.GetHashCode() ?? 0)) * 397) ^ ((Generation?.GetHashCode() ?? 0) * 31) ^ (OwnerHeroId?.GetHashCode() ?? 0);
    }
}
