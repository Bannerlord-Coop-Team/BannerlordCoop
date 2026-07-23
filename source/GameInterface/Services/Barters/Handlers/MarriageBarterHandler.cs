using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Network.Messages;
using GameInterface.Services.Barters.Messages;
using GameInterface.Services.Barters.Patches;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Messages.RomanceFlow;
using GameInterface.Services.Heroes.RomanceFlow;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Romance = TaleWorlds.CampaignSystem.Romance;

namespace GameInterface.Services.Barters.Handlers;

internal sealed class MarriageBarterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MarriageBarterHandler>();
    private static readonly TimeSpan AuthorizationLifetime = TimeSpan.FromMinutes(15);

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IRomanceAuthority romanceAuthority;
    private readonly ConversationPartyTracker conversationPartyTracker;
    private readonly LocationConversationTracker locationConversationTracker;
    private readonly IBarterClientPresentation barterClientPresentation;
    private readonly ISendCoalescer sendCoalescer;
    private readonly Dictionary<NetPeer, MarriageAuthorization> authorizations =
        new Dictionary<NetPeer, MarriageAuthorization>();

    public MarriageBarterHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IRomanceAuthority romanceAuthority,
        ConversationPartyTracker conversationPartyTracker,
        LocationConversationTracker locationConversationTracker,
        IBarterClientPresentation barterClientPresentation,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.romanceAuthority = romanceAuthority;
        this.conversationPartyTracker = conversationPartyTracker;
        this.locationConversationTracker = locationConversationTracker;
        this.barterClientPresentation = barterClientPresentation;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<NetworkAuthorizeMarriageBarter>(HandleAuthorization);
        messageBroker.Subscribe<NetworkCancelMarriageBarterAuthorization>(HandleAuthorizationCanceled);
        messageBroker.Subscribe<NetworkRequestMarriageBarter>(HandleRequest);
        messageBroker.Subscribe<NetworkMarriageBarterResult>(HandleResult);
        messageBroker.Subscribe<PlayerDisconnected>(HandlePlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAuthorizeMarriageBarter>(HandleAuthorization);
        messageBroker.Unsubscribe<NetworkCancelMarriageBarterAuthorization>(HandleAuthorizationCanceled);
        messageBroker.Unsubscribe<NetworkRequestMarriageBarter>(HandleRequest);
        messageBroker.Unsubscribe<NetworkMarriageBarterResult>(HandleResult);
        messageBroker.Unsubscribe<PlayerDisconnected>(HandlePlayerDisconnected);
        authorizations.Clear();
        MarriageBarterPatch.ClearPendingRequest();
    }

    private void HandleAuthorization(MessagePayload<NetworkAuthorizeMarriageBarter> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer)) return;

        var request = payload.What;
        GameThread.RunSafe(
            () => ProcessAuthorization(peer, request),
            context: nameof(NetworkAuthorizeMarriageBarter));
    }

    private void HandleAuthorizationCanceled(MessagePayload<NetworkCancelMarriageBarterAuthorization> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer)) return;

        var requestId = payload.What.RequestId;
        GameThread.RunSafe(() =>
        {
            if (authorizations.TryGetValue(peer, out var authorization) && authorization.RequestId == requestId)
                authorizations.Remove(peer);
        }, context: nameof(NetworkCancelMarriageBarterAuthorization));
    }

    private void HandlePlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (!ModInformation.IsServer) return;

        var peer = payload.What.PlayerId;
        GameThread.RunSafe(
            () => authorizations.Remove(peer),
            context: nameof(PlayerDisconnected));
    }

    private void HandleRequest(MessagePayload<NetworkRequestMarriageBarter> payload)
    {
        if (ModInformation.IsClient) return;

        var sender = payload.Who;
        var request = payload.What;
        GameThread.RunSafe(() => ProcessRequest(sender, request), context: nameof(MarriageBarterHandler));
    }

    private void HandleResult(MessagePayload<NetworkMarriageBarterResult> payload)
    {
        if (ModInformation.IsServer) return;

        GameThread.RunSafe(() =>
        {
            if (MarriageBarterPatch.CompleteRequest(payload.What, barterClientPresentation) && !payload.What.Accepted)
                network.SendAll(new NetworkRequestRomanceStateSync());
        }, context: nameof(NetworkMarriageBarterResult));
    }

    private void ProcessRequest(object sender, NetworkRequestMarriageBarter request)
    {
        if (!TryResolveRequester(sender, out var peer, out var player, out var playerHero, out var requesterReason))
        {
            if (peer != null)
                Reject(peer, request, 0, requesterReason);
            return;
        }

        using var playerContext = new BarterPlayerContext(
            playerHero,
            GetPlayerParty(player, playerHero)?.MobileParty);
        try
        {
            if (!TryConsumeAuthorization(peer, request))
            {
                Reject(peer, request, playerHero.Gold, "The marriage barter is no longer authorized.");
                return;
            }

            if (!TryResolveMarriageContext(
                    peer,
                    player,
                    playerHero,
                    request.CounterpartyHeroId,
                    request.Context,
                    request.ContextId,
                    request.HeroBeingProposedToId,
                    request.ProposingHeroId,
                    requireActiveConversation: false,
                    out var counterpartyHero,
                    out var heroBeingProposedTo,
                    out var proposingHero,
                    out var reason))
            {
                Reject(peer, request, playerHero.Gold, reason);
                return;
            }

            if (!TryBuildMarriageBarter(
                    player,
                    playerHero,
                    counterpartyHero,
                    heroBeingProposedTo,
                    proposingHero,
                    request.Terms,
                    out var barterData,
                    out reason))
            {
                Reject(peer, request, playerHero.Gold, reason);
                return;
            }

            var barterManager = BarterManager.Instance;
            if (barterManager == null)
            {
                Reject(peer, request, playerHero.Gold, "The marriage offer is not acceptable.");
                return;
            }

            var offerValue = barterManager.GetOfferValueForFaction(barterData, counterpartyHero.Clan);
            if (offerValue < -0.01f)
            {
                Reject(peer, request, playerHero.Gold, "The marriage offer is not acceptable.");
                return;
            }

            var offeredBarterables = barterData.GetOfferedBarterables();
            foreach (var barterable in offeredBarterables)
                barterable.Apply();
            CampaignEventDispatcher.Instance.OnBarterAccepted(playerHero, barterData.OtherHero, offeredBarterables);
            ApplyOverpayRelationBonus(playerHero, barterData.OtherHero, MathF.Max(0f, offerValue));
            if (heroBeingProposedTo.Spouse != proposingHero || proposingHero.Spouse != heroBeingProposedTo)
            {
                Reject(peer, request, playerHero.Gold, "The marriage could not be completed.");
                return;
            }

            FlushHeroGold(playerHero);
            FlushHeroGold(barterData.OtherHero);
            FlushHeroGold(heroBeingProposedTo);
            FlushHeroGold(proposingHero);
            network.Send(peer, new NetworkMarriageBarterResult(
                request.CounterpartyHeroId,
                request.HeroBeingProposedToId,
                request.ProposingHeroId,
                true,
                playerHero.Gold,
                requestId: request.RequestId));
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Failed to apply an authoritative marriage barter");
            Reject(
                peer,
                request,
                playerHero.Gold,
                "The server could not process the marriage offer.");
        }
    }

    private void ProcessAuthorization(NetPeer peer, NetworkAuthorizeMarriageBarter request)
    {
        if (string.IsNullOrEmpty(request.RequestId) ||
            !TryResolveRequester(peer, out _, out var player, out var playerHero, out _) ||
            !TryResolveMarriageContext(
                peer,
                player,
                playerHero,
                request.CounterpartyHeroId,
                request.Context,
                request.ContextId,
                request.HeroBeingProposedToId,
                request.ProposingHeroId,
                requireActiveConversation: true,
                out _,
                out _,
                out _,
                out _))
        {
            return;
        }

        authorizations[peer] = new MarriageAuthorization(
            request.RequestId,
            request.CounterpartyHeroId,
            request.Context,
            request.ContextId,
            request.HeroBeingProposedToId,
            request.ProposingHeroId,
            DateTime.UtcNow.Add(AuthorizationLifetime));
    }

    private bool TryConsumeAuthorization(NetPeer peer, NetworkRequestMarriageBarter request)
    {
        if (string.IsNullOrEmpty(request.RequestId) ||
            !authorizations.TryGetValue(peer, out var authorization))
            return false;

        if (authorization.ExpiresAtUtc <= DateTime.UtcNow)
        {
            authorizations.Remove(peer);
            return false;
        }

        if (!authorization.Matches(request))
            return false;

        authorizations.Remove(peer);
        return true;
    }

    private bool TryResolveRequester(
        object sender,
        out NetPeer peer,
        out Player player,
        out Hero playerHero,
        out string reason)
    {
        peer = sender as NetPeer;
        player = null;
        playerHero = null;
        reason = null;

        if (peer == null)
        {
            Logger.Error("Received marriage barter request without an originating peer");
            reason = "The server could not identify the marriage requester.";
            return false;
        }

        if (!playerManager.TryGetPlayer(peer, out player))
        {
            Logger.Warning("Received marriage barter request from unregistered peer {Peer}", peer.Id);
            reason = "The server could not identify the marriage requester.";
            return false;
        }

        if (!TryResolveHero(player.HeroId, out playerHero))
        {
            Logger.Warning("Unable to resolve player hero {HeroId} for peer {Peer}", player.HeroId, peer.Id);
            reason = "The server could not identify the marriage requester.";
            return false;
        }

        return true;
    }

    private bool TryResolveHero(string heroId, out Hero hero)
    {
        hero = null;
        return !string.IsNullOrEmpty(heroId) && objectManager.TryGetObject(heroId, out hero);
    }

    private bool TryResolveMarriageContext(
        NetPeer peer,
        Player player,
        Hero playerHero,
        string counterpartyHeroId,
        int contextValue,
        string contextId,
        string heroBeingProposedToId,
        string proposingHeroId,
        bool requireActiveConversation,
        out Hero counterpartyHero,
        out Hero heroBeingProposedTo,
        out Hero proposingHero,
        out string reason)
    {
        counterpartyHero = null;
        heroBeingProposedTo = null;
        proposingHero = null;
        reason = null;

        if (!Enum.IsDefined(typeof(MarriageConversationContext), contextValue) ||
            !TryResolveHero(counterpartyHeroId, out counterpartyHero) ||
            !TryResolveHero(heroBeingProposedToId, out heroBeingProposedTo) ||
            !TryResolveHero(proposingHeroId, out proposingHero))
        {
            reason = "The server could not identify the marriage participants.";
            return false;
        }

        if (requireActiveConversation &&
            !IsActiveConversation(peer, player, counterpartyHero, (MarriageConversationContext)contextValue, contextId))
        {
            reason = "The marriage conversation is no longer active.";
            return false;
        }

        if (heroBeingProposedTo == proposingHero ||
            !heroBeingProposedTo.IsAlive ||
            !proposingHero.IsAlive ||
            heroBeingProposedTo.Spouse != null ||
            proposingHero.Spouse != null ||
            heroBeingProposedTo.Clan == null ||
            proposingHero.Clan == null ||
            heroBeingProposedTo.Clan == proposingHero.Clan ||
            counterpartyHero.IsPlayerHero() ||
            counterpartyHero.IsPrisoner ||
            counterpartyHero.Clan?.Leader != counterpartyHero)
        {
            reason = "Those heroes are no longer eligible for marriage.";
            return false;
        }

        var playerClan = playerHero.Clan;
        var counterpartyClan = counterpartyHero.Clan;
        var heroBeingIsPlayerClan = heroBeingProposedTo.Clan == playerClan;
        var proposingIsPlayerClan = proposingHero.Clan == playerClan;
        var heroBeingIsCounterpartyClan = heroBeingProposedTo.Clan == counterpartyClan;
        var proposingIsCounterpartyClan = proposingHero.Clan == counterpartyClan;
        if (playerClan == null || counterpartyClan == null || playerClan == counterpartyClan ||
            heroBeingIsPlayerClan == proposingIsPlayerClan ||
            heroBeingIsCounterpartyClan == proposingIsCounterpartyClan)
        {
            reason = "The proposed marriage does not match the negotiating clans.";
            return false;
        }

        var romanticLevel = Romance.GetRomanticLevel(heroBeingProposedTo, proposingHero);
        if (proposingHero == playerHero)
        {
            if (!romanceAuthority.TryValidateMarriage(playerHero, heroBeingProposedTo, out reason))
                return false;
        }
        else
        {
            if (romanticLevel != Romance.RomanceLevelEnum.MatchMadeByFamily)
            {
                reason = "The arranged marriage has not been agreed by both clans.";
                return false;
            }

            if (heroBeingProposedTo != playerHero &&
                (playerClan.Leader != playerHero ||
                 heroBeingProposedTo.IsPlayerHero() ||
                 proposingHero.IsPlayerHero()))
            {
                reason = "The player cannot authorize that arranged marriage.";
                return false;
            }
        }

        if (FactionManager.IsAtWarAgainstFaction(playerClan.MapFaction, counterpartyClan.MapFaction) ||
            !Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(heroBeingProposedTo, proposingHero))
        {
            reason = "Those heroes are no longer eligible for marriage.";
            return false;
        }

        return true;
    }

    private bool IsActiveConversation(
        NetPeer peer,
        Player player,
        Hero counterpartyHero,
        MarriageConversationContext context,
        string contextId)
    {
        if (string.IsNullOrEmpty(contextId)) return false;

        if (context == MarriageConversationContext.Location)
        {
            return counterpartyHero.CharacterObject != null &&
                   objectManager.TryGetId(counterpartyHero.CharacterObject, out var characterId) &&
                   locationConversationTracker.TryGetEngagement(peer, out var npcKey) &&
                   npcKey == LocationConversationTracker.ComposeKey(contextId, characterId);
        }

        if (string.IsNullOrEmpty(player.MobilePartyId) ||
            !objectManager.TryGetObject(contextId, out PartyBase counterpartyParty) ||
            counterpartyParty.LeaderHero != counterpartyHero ||
            !objectManager.TryGetObject(player.MobilePartyId, out MobileParty playerParty) ||
            !objectManager.TryGetId(playerParty.Party, out var playerPartyId) ||
            !conversationPartyTracker.TryGetEngagement(peer, out var engagement))
        {
            return false;
        }

        return engagement.PartyId == contextId && engagement.EngagerPartyId == playerPartyId;
    }

    private bool TryBuildMarriageBarter(
        Player player,
        Hero playerHero,
        Hero counterpartyHero,
        Hero heroBeingProposedTo,
        Hero proposingHero,
        MarriageBarterTerm[] terms,
        out BarterData barterData,
        out string reason)
    {
        barterData = null;
        reason = null;

        var playerParty = GetPlayerParty(player, playerHero);
        var counterpartyParty = counterpartyHero.PartyBelongedTo?.Party;
        var romanticState = Romance.GetRomanticState(heroBeingProposedTo, proposingHero);
        var persuasionCostReduction = heroBeingProposedTo == playerHero || proposingHero == playerHero
            ? (int)(romanticState?.ScoreFromPersuasion ?? 0f)
            : 0;
        var marriageBarterable = new MarriageBarterable(
            playerHero,
            playerParty,
            heroBeingProposedTo,
            proposingHero);

        barterData = new BarterData(
            playerHero,
            counterpartyHero,
            playerParty,
            counterpartyParty,
            (barterable, data, _) => BarterManager.Instance.InitializeMarriageBarterContext(
                barterable,
                data,
                new Tuple<Hero, Hero>(heroBeingProposedTo, proposingHero)),
            persuasionCostReduction,
            false);

        barterData.AddBarterGroup(new DefaultsBarterGroup());
        marriageBarterable.SetIsOffered(true);
        barterData.AddBarterable<OtherBarterGroup>(marriageBarterable, true);
        marriageBarterable.SetIsOffered(true);
        CampaignEventDispatcher.Instance.OnBarterablesRequested(barterData);

        return TryApplyRequestedTerms(
            playerHero,
            counterpartyHero,
            barterData,
            terms ?? Array.Empty<MarriageBarterTerm>(),
            out reason);
    }

    private PartyBase GetPlayerParty(Player player, Hero playerHero)
    {
        if (!string.IsNullOrEmpty(player.MobilePartyId) &&
            objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var mobileParty))
        {
            return mobileParty.Party;
        }

        return playerHero.PartyBelongedTo?.Party;
    }

    private bool TryApplyRequestedTerms(
        Hero playerHero,
        Hero counterpartyHero,
        BarterData barterData,
        MarriageBarterTerm[] terms,
        out string reason)
    {
        var usedBarterables = new HashSet<Barterable>();
        foreach (var term in terms)
        {
            if (!Enum.IsDefined(typeof(MarriageBarterTermType), term.Type) || term.Amount <= 0)
            {
                reason = "The marriage offer contains an invalid term.";
                return false;
            }

            if (string.IsNullOrEmpty(term.OwnerHeroId))
            {
                reason = "The marriage offer does not identify the owner of a barter term.";
                return false;
            }

            var termType = (MarriageBarterTermType)term.Type;
            var barterable = barterData.GetBarterables().FirstOrDefault(candidate =>
                (candidate.OriginalOwner == playerHero || candidate.OriginalOwner == counterpartyHero) &&
                objectManager.TryGetId(candidate.OriginalOwner, out var ownerHeroId) &&
                ownerHeroId == term.OwnerHeroId &&
                MatchesTerm(candidate, termType, term));

            if (barterable == null || !usedBarterables.Add(barterable) || term.Amount > barterable.MaxAmount)
            {
                reason = "The marriage offer no longer matches the server's available barter terms.";
                return false;
            }

            barterable.CurrentAmount = term.Amount;
            barterable.SetIsOffered(true);
        }

        reason = null;
        return true;
    }

    private bool MatchesTerm(Barterable barterable, MarriageBarterTermType type, MarriageBarterTerm term)
    {
        switch (type)
        {
            case MarriageBarterTermType.Gold:
                return barterable is GoldBarterable;
            case MarriageBarterTermType.Item:
                return barterable is ItemBarterable itemBarterable && MatchesItem(itemBarterable, term);
            case MarriageBarterTermType.Fief:
                return barterable is FiefBarterable fiefBarterable &&
                       objectManager.TryGetId(fiefBarterable.TargetSettlement, out var settlementId) &&
                       settlementId == term.ObjectId;
            case MarriageBarterTermType.Prisoner:
                return barterable is TransferPrisonerBarterable prisonerBarterable &&
                       MatchesPrisoner(prisonerBarterable, term);
            default:
                return false;
        }
    }

    private bool MatchesItem(ItemBarterable barterable, MarriageBarterTerm term)
    {
        var equipmentElement = barterable.ItemRosterElement.EquipmentElement;
        if (!objectManager.TryGetId(equipmentElement.Item, out var itemId) || itemId != term.ObjectId)
            return false;

        var modifier = equipmentElement.ItemModifier;
        if ((modifier == null) != term.ItemModifierNull) return false;
        if (modifier == null) return true;

        return objectManager.TryGetId(modifier, out var modifierId) && modifierId == term.ItemModifierId;
    }

    private bool MatchesPrisoner(TransferPrisonerBarterable barterable, MarriageBarterTerm term)
    {
        var prisoner = barterable._prisonerCharacter;
        return prisoner?.CharacterObject != null &&
               objectManager.TryGetId(prisoner.CharacterObject, out var characterId) &&
               characterId == term.ObjectId;
    }

    private static void ApplyOverpayRelationBonus(Hero playerHero, Hero otherHero, float overpayAmount)
    {
        var campaign = Campaign.Current;
        if (otherHero == null ||
            overpayAmount <= 0f ||
            playerHero.MapFaction == null ||
            otherHero.MapFaction == null ||
            otherHero.MapFaction.IsAtWarWith(playerHero.MapFaction) ||
            campaign == null)
        {
            return;
        }

        var relation = otherHero.GetRelation(playerHero);
        var maximumRelation = MathF.Clamp(relation + 3, -100f, 100f);
        var relationBonus = 0f;
        for (var currentRelation = relation; currentRelation < maximumRelation; currentRelation++)
        {
            var cost = 1000 + ((100 * currentRelation) * currentRelation);
            if (overpayAmount >= cost)
            {
                overpayAmount -= cost;
                relationBonus += 1f;
                continue;
            }

            if (MBRandom.RandomFloat <= overpayAmount / cost)
                relationBonus += 1f;
            break;
        }

        if (playerHero.GetPerkValue(DefaultPerks.Charm.Tribute))
            relationBonus *= 1f + DefaultPerks.Charm.Tribute.PrimaryBonus;

        var roundedBonus = (int)MathF.Ceiling(relationBonus);
        if (roundedBonus > 0)
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(playerHero, otherHero, roundedBonus);
    }

    private void FlushHeroGold(Hero hero)
    {
        if (sendCoalescer == null || hero == null || !objectManager.TryGetId(hero, out var heroId)) return;
        sendCoalescer.FlushInstance(heroId, network);
    }

    private void Reject(NetPeer peer, NetworkRequestMarriageBarter request, int playerGold, string reason)
    {
        Logger.Warning(
            "Rejected marriage barter with {CounterpartyHeroId}: {Reason}",
            request.CounterpartyHeroId,
            reason);
        network.Send(peer, new NetworkMarriageBarterResult(
            request.CounterpartyHeroId,
            request.HeroBeingProposedToId,
            request.ProposingHeroId,
            false,
            playerGold,
            reason,
            request.RequestId));
    }

    private sealed class MarriageAuthorization
    {
        public string RequestId { get; }
        private string CounterpartyHeroId { get; }
        private int Context { get; }
        private string ContextId { get; }
        private string HeroBeingProposedToId { get; }
        private string ProposingHeroId { get; }
        public DateTime ExpiresAtUtc { get; }

        public MarriageAuthorization(
            string requestId,
            string counterpartyHeroId,
            int context,
            string contextId,
            string heroBeingProposedToId,
            string proposingHeroId,
            DateTime expiresAtUtc)
        {
            RequestId = requestId;
            CounterpartyHeroId = counterpartyHeroId;
            Context = context;
            ContextId = contextId;
            HeroBeingProposedToId = heroBeingProposedToId;
            ProposingHeroId = proposingHeroId;
            ExpiresAtUtc = expiresAtUtc;
        }

        public bool Matches(NetworkRequestMarriageBarter request)
            => RequestId == request.RequestId &&
               CounterpartyHeroId == request.CounterpartyHeroId &&
               Context == request.Context &&
               ContextId == request.ContextId &&
               HeroBeingProposedToId == request.HeroBeingProposedToId &&
               ProposingHeroId == request.ProposingHeroId;
    }
}
