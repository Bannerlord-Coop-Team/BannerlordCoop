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
using GameInterface.Services.Issues.Framework.Resolvers;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
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
    private readonly IIssueResolver issueResolver;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager playerManager;
    private readonly Dictionary<SessionKey, string> sessions = new();

    public PersuasionSessionService(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.ownership = ownership;
        this.playerManager = playerManager;

        messageBroker.Subscribe<PersuasionStarted>(HandleStarted);
        messageBroker.Subscribe<NetworkIssueFinalized>(HandleFinalized);
        messageBroker.Subscribe<PlayerDisconnected>(HandleDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PersuasionStarted>(HandleStarted);
        messageBroker.Unsubscribe<NetworkIssueFinalized>(HandleFinalized);
        messageBroker.Unsubscribe<PlayerDisconnected>(HandleDisconnected);
    }

    public void Start(QuestBase quest, object target)
    {
        if (ModInformation.IsServer ||
            !objectManager.TryGetIdWithLogging(target, out var targetId) ||
            !objectManager.TryGetIdWithLogging(IssueManager.GetIssueOfQuest(quest)?.IssueOwner, out var ownerId))
        {
            return;
        }

        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null)
        {
            return;
        }

        network.SendAll(new PersuasionStarted(ownerId, issue.StringId, issue.StringId, targetId));
    }

    public bool TryConsume(QuestBase quest, object target)
    {
        if (ModInformation.IsClient)
        {
            return true;
        }

        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !objectManager.TryGetId(issue.IssueOwner, out var ownerId) ||
            !objectManager.TryGetId(Hero.MainHero, out var heroId) ||
            !playerManager.Players.Any(player => player.HeroId == heroId))
        {
            return false;
        }

        var key = new SessionKey(ownerId, issue.StringId, issue.StringId, heroId);
        if (!sessions.TryGetValue(key, out var sessionTarget) ||
            !objectManager.TryGetId(target, out var targetId) || sessionTarget != targetId ||
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
            if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !issue.IsOngoing || issue.IssueQuest == null ||
                data.Generation != issue.StringId ||
                !ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out var owner) || owner != player.ControllerId ||
                !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
                !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party) ||
                !TryGetTargetParty(data.TargetId, out var targetParty) ||
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

            sessions.Add(key, data.TargetId);
        }, context: nameof(PersuasionSessionService));
    }

    private void HandleFinalized(MessagePayload<NetworkIssueFinalized> payload)
    {
        var data = payload.What;
        foreach (var key in new List<SessionKey>(sessions.Keys))
        {
            if (key.IssueOwnerId == data.IssueOwnerId && key.IssueId == data.IssueId)
            {
                sessions.Remove(key);
            }
        }
    }

    private void HandleDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (payload.What.PlayerId == null || !playerManager.TryGetPlayer(payload.What.PlayerId, out var player))
        {
            return;
        }

        foreach (var key in new List<SessionKey>(sessions.Keys))
        {
            if (key.OwnerHeroId == player.HeroId)
            {
                sessions.Remove(key);
            }
        }
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

    private static bool AreTogether(MobileParty owner, MobileParty target)
    {
        return owner.CurrentSettlement != null && owner.CurrentSettlement == target.CurrentSettlement ||
            owner.MapEvent != null && owner.MapEvent == target.MapEvent;
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
        public override int GetHashCode() => ((IssueOwnerId?.GetHashCode() ?? 0) * 397) ^ (IssueId?.GetHashCode() ?? 0) ^ (Generation?.GetHashCode() ?? 0) ^ (OwnerHeroId?.GetHashCode() ?? 0);
    }
}
