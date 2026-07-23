using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Network.Messages;
using GameInterface.Services.Barters.Messages;
using GameInterface.Services.Barters.Patches;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Barters.Handlers;

internal sealed class LordBarterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordBarterHandler>();
    private static readonly TimeSpan AuthorizationLifetime = TimeSpan.FromMinutes(15);
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly ConversationPartyTracker conversationPartyTracker;
    private readonly LocationConversationTracker locationConversationTracker;
    private readonly IBarterClientPresentation presentation;
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
        ConversationPartyTracker conversationPartyTracker,
        LocationConversationTracker locationConversationTracker,
        IBarterClientPresentation presentation,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.conversationPartyTracker = conversationPartyTracker;
        this.locationConversationTracker = locationConversationTracker;
        this.presentation = presentation;
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
        authorizations.Clear();
        completedResults.Clear();
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

            if (!TryResolveContext(peer, request, out playerHero, out var playerParty, out var targetHero, out var targetParty, out var reason))
            {
                Reject(peer, request, playerHero?.Gold ?? 0, reason);
                return;
            }

            if (!TryGetAuthorization(peer, request, out reason))
            {
                Reject(peer, request, playerHero.Gold, reason);
                return;
            }

            if (!CanAuthorizeKind(peer, playerHero, targetHero, request, out reason))
            {
                Reject(peer, request, playerHero.Gold, reason);
                return;
            }

            using var playerContext = new BarterPlayerContext(playerHero, playerParty.MobileParty);
            if (!TryBuildBarter(playerHero, playerParty, targetHero, targetParty, request, out var barter, out reason))
            {
                Reject(peer, request, playerHero.Gold, reason);
                return;
            }
            var manager = BarterManager.Instance;
            var offerValue = manager.GetOfferValueForFaction(barter, targetHero.Clan);
            if (offerValue < -0.01f)
            {
                Reject(peer, request, playerHero.Gold, "The lord will not accept this offer.");
                return;
            }

            authorizations.Remove(peer);
            mutationStarted = true;
            var offered = barter.GetOfferedBarterables();
            foreach (var barterable in offered)
            {
                if (!(barterable is SafePassageBarterable) &&
                    !(barterable is NoAttackBarterable))
                {
                    barterable.Apply();
                }
            }
            if ((LordBarterKind)request.Kind == LordBarterKind.SafePassage)
            {
                ApplySafePassage(targetParty.MobileParty, playerParty.MobileParty);
                ConversationPartyHold.EndEngagement(conversationPartyTracker, peer);
            }

            FlushGold(playerHero);
            FlushGold(targetHero);
            SendAccepted(peer, request, playerHero.Gold);
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

    private void ProcessAuthorization(NetPeer peer, NetworkAuthorizeLordBarter authorization)
    {
        if (string.IsNullOrEmpty(authorization.RequestId) ||
            !Enum.IsDefined(typeof(PeaceConversationContext), authorization.Context) ||
            !Enum.IsDefined(typeof(LordBarterKind), authorization.Kind))
        {
            return;
        }

        var request = new NetworkRequestLordBarter(
            authorization.TargetHeroId,
            (PeaceConversationContext)authorization.Context,
            authorization.ContextId,
            (LordBarterKind)authorization.Kind,
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
            DateTime.UtcNow.Add(AuthorizationLifetime));
        completedResults.Remove(peer);
    }

    private bool TryGetAuthorization(NetPeer peer, NetworkRequestLordBarter request, out string reason)
    {
        reason = null;
        if (!authorizations.TryGetValue(peer, out var authorization))
        {
            reason = "The lord barter is no longer authorized.";
            return false;
        }

        if (authorization.ExpiresAtUtc <= DateTime.UtcNow)
        {
            authorizations.Remove(peer);
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
        playerHero = null; playerParty = null; targetHero = null; targetParty = null; reason = null;
        if (string.IsNullOrEmpty(request.RequestId) || !Enum.IsDefined(typeof(PeaceConversationContext), request.Context) ||
            !Enum.IsDefined(typeof(LordBarterKind), request.Kind) || !playerManager.TryGetPlayer(peer, out Player player) ||
            !objectManager.TryGetObject(player.HeroId, out playerHero) || !objectManager.TryGetObject(player.MobilePartyId, out MobileParty mobileParty) ||
            !objectManager.TryGetObject(request.TargetHeroId, out targetHero))
        {
            reason = "The server could not identify the lord barter participants.";
            return false;
        }
        if (targetHero.IsPlayerHero() || mobileParty.LeaderHero != playerHero || !mobileParty.IsActive)
        {
            reason = "The lord barter participants are no longer available.";
            return false;
        }
        playerParty = mobileParty.Party;
        targetParty = targetHero.PartyBelongedTo?.Party;
        if ((PeaceConversationContext)request.Context == PeaceConversationContext.MapParty)
        {
            if (!objectManager.TryGetObject(request.ContextId, out PartyBase requestedParty) || requestedParty != targetParty ||
                requestedParty.MobileParty?.IsActive != true || requestedParty.MobileParty.MapEvent != null || mobileParty.MapEvent != null ||
                !objectManager.TryGetId(playerParty, out var playerPartyId) || !conversationPartyTracker.TryGetEngagement(peer, out var engagement) ||
                engagement.PartyId != request.ContextId || engagement.EngagerPartyId != playerPartyId)
            {
                reason = "The lord conversation is no longer active.";
                return false;
            }
        }
        else if (targetHero.CharacterObject == null || !objectManager.TryGetId(targetHero.CharacterObject, out var characterId) ||
                 !locationConversationTracker.TryGetEngagement(peer, out var npcKey) || npcKey != LocationConversationTracker.ComposeKey(request.ContextId, characterId))
        {
            reason = "The lord conversation is no longer active.";
            return false;
        }

        if (targetHero.IsPrisoner || targetHero.Clan == null)
        {
            reason = "That lord is no longer available for barter.";
            return false;
        }
        return true;
    }

    private bool CanAuthorizeKind(
        NetPeer peer,
        Hero playerHero,
        Hero targetHero,
        NetworkRequestLordBarter request,
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

    private bool TryBuildBarter(Hero playerHero, PartyBase playerParty, Hero targetHero, PartyBase targetParty, NetworkRequestLordBarter request, out BarterData barter, out string reason)
    {
        barter = null; reason = null;
        if (BarterManager.Instance == null)
        {
            reason = "The server barter system is unavailable.";
            return false;
        }
        var kind = (LordBarterKind)request.Kind;
        if (kind == LordBarterKind.JoinKingdomAsClan &&
            !CanAuthorizeKind(null, playerHero, targetHero, request, out reason))
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
                playerHero.Clan.Kingdom,
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
            case PeaceBarterTermType.Gold: return barterable is GoldBarterable;
            case PeaceBarterTermType.Item:
                if (!(barterable is ItemBarterable item)) return false;
                var equipment = item.ItemRosterElement.EquipmentElement;
                if (!objectManager.TryGetId(equipment.Item, out var itemId) || itemId != term.ObjectId || (equipment.ItemModifier == null) != term.ItemModifierNull) return false;
                return equipment.ItemModifier == null || (objectManager.TryGetId(equipment.ItemModifier, out var modifierId) && modifierId == term.ItemModifierId);
            case PeaceBarterTermType.Fief:
                return barterable is FiefBarterable fief && objectManager.TryGetId(fief.TargetSettlement, out var settlementId) && settlementId == term.ObjectId;
            case PeaceBarterTermType.TransferPrisoner:
                return barterable is TransferPrisonerBarterable transfer && MatchesPrisoner(transfer._prisonerCharacter, term);
            case PeaceBarterTermType.ReleasePrisoner:
                return barterable is SetPrisonerFreeBarterable release && MatchesPrisoner(release._prisonerCharacter, term);
            default: return false;
        }
    }

    private bool MatchesPrisoner(Hero prisoner, PeaceBarterTerm term) => prisoner?.CharacterObject != null && objectManager.TryGetId(prisoner.CharacterObject, out var id) && id == term.ObjectId;

    private static void ApplySafePassage(MobileParty targetParty, MobileParty playerParty)
    {
        if (targetParty == null || playerParty == null) return;
        var attackProtectionEnds = CampaignTime.HoursFromNow(32f);
        var factionProtectionEnds = CampaignTime.DaysFromNow(5f);
        var protectedParties = new HashSet<MobileParty> { targetParty };
        if (targetParty.Army?.LeaderParty != null) protectedParties.Add(targetParty.Army.LeaderParty);
        foreach (var party in protectedParties.ToArray())
        {
            if (party.AttachedParties == null) continue;
            foreach (var attachedParty in party.AttachedParties)
                if (attachedParty?.IsActive == true) protectedParties.Add(attachedParty);
        }
        foreach (var party in protectedParties)
        {
            DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
                party,
                playerParty,
                attackProtectionEnds);
            party.SetMoveModeHold();
            party.IgnoreForHours(32f);
            party.Ai.SetInitiative(0f, 0.8f, 8f);
        }
        DefaultMobilePartyAIModelPatches.PreventFactionAttacksUntil(
            playerParty,
            targetParty.MapFaction,
            factionProtectionEnds);
    }

    private void FlushGold(Hero hero)
    {
        if (sendCoalescer != null && hero != null && objectManager.TryGetId(hero, out var id)) sendCoalescer.FlushInstance(id, network);
    }

    private void Reject(NetPeer peer, NetworkRequestLordBarter request, int gold, string reason)
    {
        Logger.Warning("Rejected lord barter with {TargetHeroId}: {Reason}", request.TargetHeroId, reason);
        SendResult(peer, new NetworkLordBarterResult(request.ContextId, false, gold, reason, request.RequestId));
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
        public DateTime ExpiresAtUtc { get; }

        public LordBarterAuthorization(
            string requestId,
            string targetHeroId,
            int context,
            string contextId,
            int kind,
            DateTime expiresAtUtc)
        {
            RequestId = requestId;
            TargetHeroId = targetHeroId;
            Context = context;
            ContextId = contextId;
            Kind = kind;
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
