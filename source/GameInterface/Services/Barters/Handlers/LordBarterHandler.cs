using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Network.Messages;
using GameInterface.Services.Barters.Messages;
using GameInterface.Services.Barters.Patches;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Interfaces;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Barters.Handlers;

internal sealed partial class LordBarterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordBarterHandler>();
    private static readonly TimeSpan AuthorizationLifetime = TimeSpan.FromMinutes(15);
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IKingdomMembershipState kingdomMembershipState;
    private readonly ConversationPartyTracker conversationPartyTracker;
    private readonly LocationConversationTracker locationConversationTracker;
    private readonly IBarterClientPresentation presentation;
    private readonly ISafePassagePartyResolver safePassagePartyResolver;
    private readonly ISiegeEventInterface siegeEventInterface;
    private readonly ISendCoalescer sendCoalescer;
    private readonly Dictionary<NetPeer, LordBarterAuthorization> authorizations =
        new Dictionary<NetPeer, LordBarterAuthorization>();
    private readonly Dictionary<NetPeer, NetworkLordBarterResult> completedResults =
        new Dictionary<NetPeer, NetworkLordBarterResult>();

    public LordBarterHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IKingdomMembershipState kingdomMembershipState,
        ConversationPartyTracker conversationPartyTracker,
        LocationConversationTracker locationConversationTracker,
        IBarterClientPresentation presentation,
        ISafePassagePartyResolver safePassagePartyResolver,
        ISiegeEventInterface siegeEventInterface,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.kingdomMembershipState = kingdomMembershipState;
        this.conversationPartyTracker = conversationPartyTracker;
        this.locationConversationTracker = locationConversationTracker;
        this.presentation = presentation;
        this.safePassagePartyResolver = safePassagePartyResolver;
        this.siegeEventInterface = siegeEventInterface;
        this.sendCoalescer = sendCoalescer;
        messageBroker.Subscribe<NetworkAuthorizeLordBarter>(HandleAuthorization);
        messageBroker.Subscribe<NetworkCancelLordBarterAuthorization>(HandleAuthorizationCanceled);
        messageBroker.Subscribe<NetworkRequestLordBarter>(HandleRequest);
        messageBroker.Subscribe<NetworkLordBarterResult>(HandleResult);
        messageBroker.Subscribe<PlayerDisconnected>(HandlePlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAuthorizeLordBarter>(HandleAuthorization);
        messageBroker.Unsubscribe<NetworkCancelLordBarterAuthorization>(HandleAuthorizationCanceled);
        messageBroker.Unsubscribe<NetworkRequestLordBarter>(HandleRequest);
        messageBroker.Unsubscribe<NetworkLordBarterResult>(HandleResult);
        messageBroker.Unsubscribe<PlayerDisconnected>(HandlePlayerDisconnected);

        // Every other access to these dictionaries happens on the game thread (handlers are drained
        // there), so clearing them from the disposing thread would be a data race - marshal instead.
        // NOT blocking: Dispose runs during container teardown, when the game loop may already have
        // stopped pumping, and a blocking wait there just times out after 30s and fails the teardown.
        // If the queued clear never runs, the handler is being discarded anyway.
        GameThread.RunSafe(() =>
        {
            authorizations.Clear();
            completedResults.Clear();
        },
            blocking: false,
            context: nameof(LordBarterHandler));

        LordBarterPatch.ClearPendingRequest();
    }

    private void HandleAuthorization(MessagePayload<NetworkAuthorizeLordBarter> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer)) return;
        var request = payload.What;
        GameThread.RunSafe(() => ProcessAuthorization(peer, request), context: nameof(NetworkAuthorizeLordBarter));
    }

    private void HandleAuthorizationCanceled(MessagePayload<NetworkCancelLordBarterAuthorization> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer)) return;
        var requestId = payload.What.RequestId;
        GameThread.RunSafe(() =>
        {
            if (authorizations.TryGetValue(peer, out var authorization) && authorization.RequestId == requestId)
                authorizations.Remove(peer);
        }, context: nameof(NetworkCancelLordBarterAuthorization));
    }

    private void HandlePlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (!ModInformation.IsServer) return;
        var peer = payload.What.PlayerId;
        GameThread.RunSafe(() =>
        {
            authorizations.Remove(peer);
            completedResults.Remove(peer);
        }, context: nameof(PlayerDisconnected));
    }

    private void HandleRequest(MessagePayload<NetworkRequestLordBarter> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer)) return;
        var request = payload.What;
        GameThread.RunSafe(() => ProcessRequest(peer, request), context: nameof(LordBarterHandler));
    }

    private void HandleResult(MessagePayload<NetworkLordBarterResult> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() => LordBarterPatch.CompleteRequest(payload.What, presentation), context: nameof(NetworkLordBarterResult));
    }

    /// <summary>Everything the apply phase needs, resolved and validated exactly once.</summary>
    private readonly struct LordBarterContext
    {
        public readonly PartyBase PlayerParty;
        public readonly Hero TargetHero;
        public readonly PartyBase TargetParty;
        public readonly Kingdom TargetKingdom;

        public LordBarterContext(PartyBase playerParty, Hero targetHero, PartyBase targetParty, Kingdom targetKingdom)
        {
            PlayerParty = playerParty;
            TargetHero = targetHero;
            TargetParty = targetParty;
            TargetKingdom = targetKingdom;
        }
    }

    private void ProcessRequest(NetPeer peer, NetworkRequestLordBarter request)
    {
        Hero playerHero = null;
        var mutationStarted = false;
        try
        {
            if (completedResults.TryGetValue(peer, out var completed) &&
                completed.RequestId == request.RequestId)
            {
                SendResult(peer, completed);
                return;
            }

            if (!TryAuthorizeRequest(peer, request, out var context, out playerHero)) return;

            // Scoped across the whole apply: the barterables read the player gold and party through it.
            using var playerContext = new BarterPlayerContext(playerHero, context.PlayerParty.MobileParty);

            if (!TryBuildBarter(
                    playerHero, context.PlayerParty, context.TargetHero, context.TargetParty,
                    request, context.TargetKingdom, out var barter, out var reason))
            {
                Reject(peer, request, playerHero.Gold, reason);
                return;
            }

            if (!TryPriceOffer(peer, request, context, playerHero, barter, out var offerValue, out var safePassageOpponents))
                return;

            authorizations.Remove(peer);
            mutationStarted = true;

            ApplyBarter(peer, request, context, playerHero, barter, offerValue, safePassageOpponents);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Failed to apply authoritative lord barter");
            if (mutationStarted)
            {
                SendAccepted(peer, request, playerHero?.Gold ?? 0);
                return;
            }

            Reject(peer, request, playerHero?.Gold ?? 0, "The server could not process the lord barter.");
        }
    }

    /// <summary>Resolves the participants and checks this peer is allowed the kind of barter it asked for.</summary>
    private bool TryAuthorizeRequest(
        NetPeer peer, NetworkRequestLordBarter request, out LordBarterContext context, out Hero playerHero)
    {
        context = default;

        if (!TryResolveContext(peer, request, out playerHero, out var playerParty, out var targetHero, out var targetParty, out var reason))
        {
            Reject(peer, request, playerHero?.Gold ?? 0, reason);
            return false;
        }

        if (!TryGetAuthorization(peer, request, out var authorization, out reason))
        {
            Reject(peer, request, playerHero.Gold, reason);
            return false;
        }

        Kingdom targetKingdom = null;
        if ((LordBarterKind)request.Kind == LordBarterKind.JoinKingdomAsClan &&
            !objectManager.TryGetObject(authorization.TargetKingdomId, out targetKingdom))
        {
            Reject(peer, request, playerHero.Gold, "The destination kingdom is no longer available.");
            return false;
        }

        if (!CanAuthorizeKind(peer, playerHero, targetHero, request, targetKingdom, out reason))
        {
            Reject(peer, request, playerHero.Gold, reason);
            return false;
        }

        context = new LordBarterContext(playerParty, targetHero, targetParty, targetKingdom);
        return true;
    }

    /// <summary>
    /// Values the offer and refuses it if the lord will not take it. Rejecting here is the normal outcome of
    /// a bad deal, not an error.
    /// </summary>
    private bool TryPriceOffer(
        NetPeer peer, NetworkRequestLordBarter request, in LordBarterContext context, Hero playerHero,
        BarterData barter, out float offerValue, out IReadOnlyList<MobileParty> safePassageOpponents)
    {
        safePassageOpponents = Array.Empty<MobileParty>();

        if ((LordBarterKind)request.Kind == LordBarterKind.SafePassage)
        {
            var safePassageOffer = EvaluateSafePassageOffer(
                barter, playerHero, context.PlayerParty.MobileParty, context.TargetHero, context.TargetParty.MobileParty);
            safePassageOpponents = safePassageOffer.OpponentParties;
            offerValue = safePassageOffer.OfferValue;
        }
        else
        {
            offerValue = BarterManager.Instance.GetOfferValueForFaction(barter, context.TargetHero.Clan);
        }

        if (offerValue >= -0.01f) return true;

        // The client barter UI auto-balances the offer to land the total at exactly the acceptance boundary
        // (BarterVM.AutoBalanceAdd, fulfillRatio 1f), so it always shows the deal as acceptable at the
        // minimum price. Both sides then run the SAME test (GetOfferValueForFaction vs targetHero.Clan,
        // threshold -0.01f) - but any drift in the inputs it reads, above all Kingdom._clans /
        // Kingdom._fiefsCache, moves the result. Those feed a quadratic term in DefaultDiplomacyModel
        // (10000 - 100 * sum(WarPartyLimit)^2), so a roster difference of a couple of clans is worth
        // hundreds of thousands of denars - and a one-denar gap at the boundary flips accept into reject.
        //
        // Log the number so this is diagnosable, and tell the player the shortfall instead of a flat refusal
        // they have no way to act on.
        LogOfferValueBreakdown(playerHero, context.TargetHero, context.TargetKingdom, barter, offerValue);

        var shortfall = (int)Math.Ceiling(-offerValue);
        Reject(
            peer,
            request,
            playerHero.Gold,
            $"The lord will not accept this offer - it is short by about {shortfall} denars. Offer more than the suggested amount.");
        return false;
    }

    /// <summary>Past this point the deal is going through; anything that throws is still reported accepted.</summary>
    private void ApplyBarter(
        NetPeer peer, NetworkRequestLordBarter request, in LordBarterContext context, Hero playerHero,
        BarterData barter, float offerValue, IReadOnlyList<MobileParty> safePassageOpponents)
    {
        var isSafePassage = (LordBarterKind)request.Kind == LordBarterKind.SafePassage;

        // Captured before Apply(), which is what moves the clan.
        var joinTargetClan = (LordBarterKind)request.Kind == LordBarterKind.JoinKingdomAsClan
            ? context.TargetHero.Clan
            : null;
        var joinPreviousKingdom = joinTargetClan?.Kingdom;

        var offered = barter.GetOfferedBarterables();
        foreach (var barterable in offered)
        {
            if (!(barterable is SafePassageBarterable) && !(barterable is NoAttackBarterable))
            {
                barterable.Apply();
            }
        }

        if (joinTargetClan != null)
            ApplyDefection(playerHero, request, joinTargetClan, joinPreviousKingdom, context.TargetKingdom);

        if (isSafePassage)
            ApplySafePassage(context.TargetParty?.MobileParty, context.PlayerParty?.MobileParty, safePassageOpponents);

        CampaignEventDispatcher.Instance.OnBarterAccepted(playerHero, context.TargetHero, offered);
        ApplyOverpayRelationBonus(playerHero, context.TargetHero, offerValue);

        if (isSafePassage)
            ConversationPartyHold.EndEngagement(conversationPartyTracker, peer);

        FlushGold(playerHero);
        FlushGold(context.TargetHero);
        FlushHeroDeveloper(playerHero);
        SendAccepted(peer, request, playerHero.Gold);
    }

    /// <summary>
    /// Clan._kingdom is AutoSynced, but the Kingdom._clans / fief collections are not reliably intercepted,
    /// so clients would see the clan claim the kingdom while the kingdom's own roster still omitted it. Every
    /// other membership mutation in the repo republishes explicitly (see VassalServiceHandler.ApplyVassalage).
    /// </summary>
    private void ApplyDefection(
        Hero playerHero, NetworkRequestLordBarter request, Clan joinTargetClan,
        Kingdom joinPreviousKingdom, Kingdom targetKingdom)
    {
        kingdomMembershipState.MoveClanToKingdom(
            joinPreviousKingdom,
            targetKingdom,
            joinTargetClan,
            publishCollectionChanges: true,
            republishExistingCollections: true);

        if (targetKingdom != null && !targetKingdom.Clans.Contains(joinTargetClan))
        {
            Logger.Error(
                "Lord defection did not add clan {Clan} to kingdom {Kingdom} collections",
                joinTargetClan.StringId,
                targetKingdom.StringId);
        }

        ApplyDefectionPersuasionXp(playerHero, request.PersuasionOutcomes);
    }

    private void ProcessAuthorization(NetPeer peer, NetworkAuthorizeLordBarter authorization)
    {
        if (string.IsNullOrEmpty(authorization.RequestId) ||
            !Enum.IsDefined(typeof(PeaceConversationContext), authorization.Context) ||
            !Enum.IsDefined(typeof(LordBarterKind), authorization.Kind))
        {
            return;
        }

        var kind = (LordBarterKind)authorization.Kind;
        Kingdom targetKingdom = null;
        if (kind == LordBarterKind.JoinKingdomAsClan)
        {
            if (string.IsNullOrEmpty(authorization.TargetKingdomId) ||
                !objectManager.TryGetObject(authorization.TargetKingdomId, out targetKingdom))
            {
                return;
            }
        }
        else if (!string.IsNullOrEmpty(authorization.TargetKingdomId))
        {
            return;
        }

        var request = new NetworkRequestLordBarter(
            authorization.TargetHeroId,
            (PeaceConversationContext)authorization.Context,
            authorization.ContextId,
            kind,
            Array.Empty<PeaceBarterTerm>(),
            authorization.RequestId);
        if (!TryResolveContext(
                peer,
                request,
                out var playerHero,
                out _,
                out var targetHero,
                out _,
                out _) ||
            !CanAuthorizeKind(
                peer,
                playerHero,
                targetHero,
                request,
                targetKingdom,
                out _))
        {
            return;
        }

        authorizations[peer] = new LordBarterAuthorization(
            authorization.RequestId,
            authorization.TargetHeroId,
            authorization.Context,
            authorization.ContextId,
            authorization.Kind,
            authorization.TargetKingdomId,
            DateTime.UtcNow.Add(AuthorizationLifetime));
        completedResults.Remove(peer);
    }

    private bool TryGetAuthorization(
        NetPeer peer,
        NetworkRequestLordBarter request,
        out LordBarterAuthorization authorization,
        out string reason)
    {
        reason = null;
        if (!authorizations.TryGetValue(peer, out authorization))
        {
            reason = "The lord barter is no longer authorized.";
            return false;
        }

        if (authorization.ExpiresAtUtc <= DateTime.UtcNow)
        {
            authorizations.Remove(peer);
            authorization = null;
            reason = "The lord barter authorization expired.";
            return false;
        }

        if (!authorization.Matches(request))
        {
            reason = "The lord barter authorization does not match this offer.";
            return false;
        }

        return true;
    }


    private bool TryResolveContext(NetPeer peer, NetworkRequestLordBarter request, out Hero playerHero, out PartyBase playerParty, out Hero targetHero, out PartyBase targetParty, out string reason)
    {
        playerHero = null;
        playerParty = null;
        targetHero = null;
        targetParty = null;
        reason = null;

        if (!IsWellFormedRequest(request))
        {
            reason = "The server received an invalid lord barter request format.";
            return false;
        }

        if (!TryResolveParticipants(peer, request, out playerHero, out var mobileParty, out targetHero))
        {
            reason = "The server could not identify the lord barter participants.";
            return false;
        }

        if (!ParticipantsAreAvailable(playerHero, targetHero, mobileParty))
        {
            reason = "The lord barter participants are no longer available.";
            return false;
        }

        playerParty = mobileParty.Party;
        targetParty = targetHero.PartyBelongedTo?.Party;

        if (!ConversationIsStillLive(peer, request, playerParty, mobileParty, targetHero, targetParty, out reason))
            return false;

        if (targetHero.IsPrisoner || targetHero.Clan == null)
        {
            reason = "That lord is no longer available for barter.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Persuasion outcomes only belong on a defection, and the count is bounded so a tampered client cannot
    /// claim an arbitrary number of successful attempts. Rejected outright rather than truncated, so a
    /// malformed request fails loudly instead of earning partial credit.
    /// </summary>
    private static bool IsWellFormedRequest(NetworkRequestLordBarter request)
    {
        if (string.IsNullOrEmpty(request.RequestId)) return false;
        if (!Enum.IsDefined(typeof(PeaceConversationContext), request.Context)) return false;
        if (!Enum.IsDefined(typeof(LordBarterKind), request.Kind)) return false;
        if (request.PersuasionOutcomes == null || request.PersuasionOutcomes.Length == 0) return true;

        return request.PersuasionOutcomes.Length <= LordBarterPatch.MaxDefectionPersuasionOutcomes
            && (LordBarterKind)request.Kind == LordBarterKind.JoinKingdomAsClan;
    }

    private bool TryResolveParticipants(
        NetPeer peer, NetworkRequestLordBarter request,
        out Hero playerHero, out MobileParty mobileParty, out Hero targetHero)
    {
        playerHero = null;
        mobileParty = null;
        targetHero = null;

        return playerManager.TryGetPlayer(peer, out Player player)
            && objectManager.TryGetObject(player.HeroId, out playerHero)
            && objectManager.TryGetObject(player.MobilePartyId, out mobileParty)
            && objectManager.TryGetObject(request.TargetHeroId, out targetHero);
    }

    /// <summary>
    /// IsAlive matches MarriageBarterHandler's participant check: a hero can die between the authorization
    /// and the request, and every barterable dereferences both heroes.
    /// </summary>
    private static bool ParticipantsAreAvailable(Hero playerHero, Hero targetHero, MobileParty mobileParty)
        => !targetHero.IsPlayerHero()
            && targetHero.IsAlive
            && playerHero.IsAlive
            && mobileParty.LeaderHero == playerHero
            && mobileParty.IsActive;

    private bool ConversationIsStillLive(
        NetPeer peer, NetworkRequestLordBarter request, PartyBase playerParty,
        MobileParty mobileParty, Hero targetHero, PartyBase targetParty, out string reason)
    {
        reason = null;

        switch ((PeaceConversationContext)request.Context)
        {
            case PeaceConversationContext.Settlement:
                if (SettlementConversationIsLive(request, mobileParty, targetHero)) return true;
                reason = "The lord settlement conversation is no longer active.";
                return false;

            case PeaceConversationContext.MapParty:
                if (MapPartyConversationIsLive(peer, request, playerParty, mobileParty, targetParty)) return true;
                reason = "The lord conversation is no longer active.";
                return false;

            case PeaceConversationContext.Location:
                if (LocationConversationIsLive(peer, request, targetHero)) return true;
                reason = "The lord conversation is no longer active.";
                return false;

            default:
                // Refused rather than treated as a Location conversation - accepting a context we do
                // not understand is how an unvalidated barter gets through.
                reason = "The lord conversation context is not supported.";
                return false;
        }
    }

    /// <summary>
    /// A settlement-menu conversation acquires no engagement - there is no agent and no location mission to
    /// lock - so authority comes from co-location instead: both the requesting party and the target must
    /// actually be inside the settlement named by the request. That is as strong as the hold for this case,
    /// because a player who is not in the settlement cannot be talking to someone who is.
    /// </summary>
    private bool SettlementConversationIsLive(NetworkRequestLordBarter request, MobileParty mobileParty, Hero targetHero)
        => objectManager.TryGetObject(request.ContextId, out Settlement conversationSettlement)
            && mobileParty.CurrentSettlement == conversationSettlement
            && targetHero.CurrentSettlement == conversationSettlement;

    private bool MapPartyConversationIsLive(
        NetPeer peer, NetworkRequestLordBarter request, PartyBase playerParty,
        MobileParty mobileParty, PartyBase targetParty)
    {
        if (!objectManager.TryGetObject(request.ContextId, out PartyBase requestedParty)) return false;
        if (requestedParty != targetParty) return false;
        if (requestedParty.MobileParty?.IsActive != true) return false;
        if (requestedParty.MobileParty.MapEvent != null || mobileParty.MapEvent != null) return false;
        if (!objectManager.TryGetId(playerParty, out var playerPartyId)) return false;
        if (!conversationPartyTracker.TryGetEngagement(peer, out var engagement)) return false;

        return engagement.PartyId == request.ContextId && engagement.EngagerPartyId == playerPartyId;
    }

    private bool LocationConversationIsLive(NetPeer peer, NetworkRequestLordBarter request, Hero targetHero)
        => targetHero.CharacterObject != null
            && objectManager.TryGetId(targetHero.CharacterObject, out var characterId)
            && locationConversationTracker.TryGetEngagement(peer, out var npcKey)
            && npcKey == LocationConversationTracker.ComposeKey(request.ContextId, characterId);

    private bool CanAuthorizeKind(
        NetPeer peer,
        Hero playerHero,
        Hero targetHero,
        NetworkRequestLordBarter request,
        Kingdom targetKingdom,
        out string reason)
    {
        reason = null;
        var kind = (LordBarterKind)request.Kind;
        if (kind == LordBarterKind.Generic)
            return true;

        if (kind == LordBarterKind.SafePassage)
        {
            if ((PeaceConversationContext)request.Context != PeaceConversationContext.MapParty ||
                playerHero.MapFaction == null ||
                targetHero.MapFaction == null ||
                !FactionManager.IsAtWarAgainstFaction(playerHero.MapFaction, targetHero.MapFaction) ||
                !conversationPartyTracker.TryGetEngagement(peer, out var engagement) ||
                !engagement.EngagerIsDefender)
            {
                reason = "This encounter is not eligible for a safe-passage barter.";
                return false;
            }

            return true;
        }

        var playerClan = playerHero.Clan;
        var targetClan = targetHero.Clan;
        if (playerClan?.Kingdom == null ||
            targetKingdom == null ||
            playerClan.Kingdom != targetKingdom ||
            playerClan.Leader != playerHero ||
            targetClan?.Leader != targetHero ||
            targetClan.Kingdom == null ||
            targetClan.Kingdom == playerClan.Kingdom ||
            targetClan.IsMinorFaction ||
            targetClan.IsRebelClan ||
            targetClan.IsUnderMercenaryService)
        {
            reason = "Those clans are not eligible for a kingdom defection.";
            return false;
        }

        return true;
    }

    private bool TryBuildBarter(
        Hero playerHero,
        PartyBase playerParty,
        Hero targetHero,
        PartyBase targetParty,
        NetworkRequestLordBarter request,
        Kingdom targetKingdom,
        out BarterData barter,
        out string reason)
    {
        barter = null; reason = null;
        if (BarterManager.Instance == null)
        {
            reason = "The server barter system is unavailable.";
            return false;
        }
        var kind = (LordBarterKind)request.Kind;
        if (kind == LordBarterKind.JoinKingdomAsClan &&
            !CanAuthorizeKind(null, playerHero, targetHero, request, targetKingdom, out reason))
            return false;
        if (kind == LordBarterKind.SafePassage && targetParty?.MobileParty == null)
        {
            reason = "The safe-passage party is no longer available.";
            return false;
        }

        BarterManager.BarterContextInitializer initializer = null;
        var baseBarterables = new List<Barterable>();
        if (kind == LordBarterKind.SafePassage)
        {
            initializer = BarterManager.Instance.InitializeSafePassageBarterContext;
            baseBarterables.Add(new SafePassageBarterable(targetHero, playerHero, targetParty, playerParty));
            baseBarterables.Add(new NoAttackBarterable(playerHero, targetHero, playerParty, targetParty, CampaignTime.Days(5f)));
        }
        else if (kind == LordBarterKind.JoinKingdomAsClan)
        {
            initializer = BarterManager.Instance.InitializeJoinFactionBarterContext;
            baseBarterables.Add(new JoinKingdomAsClanBarterable(
                targetHero,
                targetKingdom,
                isDefecting: true));
        }

        barter = new BarterData(playerHero, targetHero, playerParty, targetParty, initializer);
        barter.AddBarterGroup(new DefaultsBarterGroup());
        foreach (var baseBarterable in baseBarterables)
        {
            baseBarterable.SetIsOffered(true);
            barter.AddBarterable<DefaultsBarterGroup>(baseBarterable, true);
        }
        CampaignEventDispatcher.Instance.OnBarterablesRequested(barter);
        return TryApplyTerms(playerHero, targetHero, barter, request.Terms, out reason);
    }

    private bool TryApplyTerms(Hero playerHero, Hero targetHero, BarterData barter, IEnumerable<PeaceBarterTerm> terms, out string reason)
    {
        var used = new HashSet<Barterable>();
        foreach (var term in terms ?? Array.Empty<PeaceBarterTerm>())
        {
            if (!Enum.IsDefined(typeof(PeaceBarterTermType), term.Type) || term.Amount <= 0 || string.IsNullOrEmpty(term.OwnerHeroId))
            {
                reason = "The lord barter contains an invalid term.";
                return false;
            }
            var type = (PeaceBarterTermType)term.Type;
            var barterable = barter.GetBarterables().FirstOrDefault(candidate =>
                (candidate.OriginalOwner == playerHero || candidate.OriginalOwner == targetHero) &&
                objectManager.TryGetId(candidate.OriginalOwner, out var ownerId) && ownerId == term.OwnerHeroId && Matches(candidate, type, term));
            if (barterable == null || !used.Add(barterable) || term.Amount > barterable.MaxAmount)
            {
                reason = "The lord barter no longer matches the server's available terms.";
                return false;
            }
            barterable.CurrentAmount = term.Amount;
            barterable.SetIsOffered(true);
        }
        reason = null;
        return true;
    }

    private bool Matches(Barterable barterable, PeaceBarterTermType type, PeaceBarterTerm term)
    {
        switch (type)
        {
            case PeaceBarterTermType.Gold:
                return barterable is GoldBarterable;
            case PeaceBarterTermType.Item:
                return barterable is ItemBarterable item && MatchesItem(item, term);
            case PeaceBarterTermType.Fief:
                return barterable is FiefBarterable fief && MatchesObject(fief.TargetSettlement, term);
            case PeaceBarterTermType.TransferPrisoner:
                return barterable is TransferPrisonerBarterable transfer && MatchesPrisoner(transfer._prisonerCharacter, term);
            case PeaceBarterTermType.ReleasePrisoner:
                return barterable is SetPrisonerFreeBarterable release && MatchesPrisoner(release._prisonerCharacter, term);
            default:
                return false;
        }
    }

    private bool MatchesItem(ItemBarterable item, PeaceBarterTerm term)
    {
        var equipment = item.ItemRosterElement.EquipmentElement;

        if (!MatchesObject(equipment.Item, term)) return false;
        // The term records whether the client's item had a modifier; a mismatch means a different item.
        if ((equipment.ItemModifier == null) != term.ItemModifierNull) return false;

        return equipment.ItemModifier == null ||
            (objectManager.TryGetId(equipment.ItemModifier, out var modifierId) && modifierId == term.ItemModifierId);
    }

    private bool MatchesObject(MBObjectBase gameObject, PeaceBarterTerm term) =>
        objectManager.TryGetId(gameObject, out var id) && id == term.ObjectId;

    private bool MatchesPrisoner(Hero prisoner, PeaceBarterTerm term) =>
        prisoner?.CharacterObject != null && MatchesObject(prisoner.CharacterObject, term);

    internal static void ApplyOverpayRelationBonus(Hero playerHero, Hero otherHero, float overpayAmount)
    {
        var campaign = Campaign.Current;
        if (otherHero == null ||
            overpayAmount <= 0f ||
            playerHero?.MapFaction == null ||
            otherHero.MapFaction == null ||
            otherHero.MapFaction.IsAtWarWith(playerHero.MapFaction) ||
            campaign?.Models?.BarterModel == null)
        {
            return;
        }

        var relationBonus = campaign.Models.BarterModel
            .CalculateOverpayRelationIncreaseCosts(otherHero, overpayAmount);
        if (relationBonus > 0)
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(playerHero, otherHero, relationBonus);
    }

    private void FlushGold(Hero hero)
    {
        if (sendCoalescer != null && hero != null && objectManager.TryGetId(hero, out var id)) sendCoalescer.FlushInstance(id, network);
    }

    // Both the Charm XP and the total XP are coalesced dictionary upserts on HeroDeveloper, so
    // without this the recruiter sees no skill change until the next coalescer tick - long after the
    // barter result lands.
    private void FlushHeroDeveloper(Hero hero)
    {
        if (sendCoalescer != null && hero?.HeroDeveloper != null &&
            objectManager.TryGetId(hero.HeroDeveloper, out var id))
            sendCoalescer.FlushInstance(id, network);
    }

    private void Reject(NetPeer peer, NetworkRequestLordBarter request, int gold, string reason)
    {
        Logger.Warning("Rejected lord barter with {TargetHeroId}: {Reason}", request.TargetHeroId, reason);
        var result = new NetworkLordBarterResult(request.ContextId, false, gold, reason, request.RequestId);
        SendResult(peer, result);
    }

    private void SendAccepted(NetPeer peer, NetworkRequestLordBarter request, int gold)
    {
        var result = new NetworkLordBarterResult(
            request.ContextId,
            true,
            gold,
            null,
            request.RequestId);
        completedResults[peer] = result;
        SendResult(peer, result);
    }

    private void SendResult(NetPeer peer, NetworkLordBarterResult result)
    {
        try
        {
            network.Send(peer, result);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Failed to send authoritative lord barter result");
        }
    }

    private sealed class LordBarterAuthorization
    {
        public string RequestId { get; }
        private string TargetHeroId { get; }
        private int Context { get; }
        private string ContextId { get; }
        private int Kind { get; }
        public string TargetKingdomId { get; }
        public DateTime ExpiresAtUtc { get; }

        public LordBarterAuthorization(
            string requestId,
            string targetHeroId,
            int context,
            string contextId,
            int kind,
            string targetKingdomId,
            DateTime expiresAtUtc)
        {
            RequestId = requestId;
            TargetHeroId = targetHeroId;
            Context = context;
            ContextId = contextId;
            Kind = kind;
            TargetKingdomId = targetKingdomId;
            ExpiresAtUtc = expiresAtUtc;
        }

        public bool Matches(NetworkRequestLordBarter request)
        {
            return request.RequestId == RequestId &&
                   request.TargetHeroId == TargetHeroId &&
                   request.Context == Context &&
                   request.ContextId == ContextId &&
                   request.Kind == Kind;
        }
    }
}
