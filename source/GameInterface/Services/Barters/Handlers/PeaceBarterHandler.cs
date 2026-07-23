using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.Barters.Messages;
using GameInterface.Services.Barters.Patches;
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

namespace GameInterface.Services.Barters.Handlers;

internal sealed class PeaceBarterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PeaceBarterHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly ConversationPartyTracker conversationPartyTracker;
    private readonly LocationConversationTracker locationConversationTracker;
    private readonly IBarterClientPresentation barterClientPresentation;
    private readonly ISendCoalescer sendCoalescer;

    public PeaceBarterHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        ConversationPartyTracker conversationPartyTracker,
        LocationConversationTracker locationConversationTracker,
        IBarterClientPresentation barterClientPresentation,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.conversationPartyTracker = conversationPartyTracker;
        this.locationConversationTracker = locationConversationTracker;
        this.barterClientPresentation = barterClientPresentation;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<NetworkRequestPeaceBarter>(HandleRequest);
        messageBroker.Subscribe<NetworkPeaceBarterResult>(HandleResult);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestPeaceBarter>(HandleRequest);
        messageBroker.Unsubscribe<NetworkPeaceBarterResult>(HandleResult);
        PeaceBarterPatch.ClearPendingRequest();
    }

    private void HandleRequest(MessagePayload<NetworkRequestPeaceBarter> payload)
    {
        if (ModInformation.IsClient) return;
        if (!(payload.Who is NetPeer peer))
        {
            Logger.Error("Received peace barter request without an originating peer");
            return;
        }

        var request = payload.What;
        GameThread.RunSafe(
            () => ProcessRequest(peer, request),
            context: nameof(PeaceBarterHandler));
    }

    private void HandleResult(MessagePayload<NetworkPeaceBarterResult> payload)
    {
        if (ModInformation.IsServer) return;

        var result = payload.What;
        GameThread.RunSafe(
            () => PeaceBarterPatch.CompleteRequest(result, barterClientPresentation),
            context: nameof(NetworkPeaceBarterResult));
    }

    private void ProcessRequest(NetPeer peer, NetworkRequestPeaceBarter request)
    {
        Hero playerHero = null;
        BarterPlayerContext playerContext = null;
        try
        {
            if (!TryResolveContext(
                    peer,
                    request,
                    out playerHero,
                    out var playerParty,
                    out var targetHero,
                    out var targetParty,
                    out var reason))
            {
                Reject(peer, request, playerHero?.Gold ?? 0, reason);
                return;
            }

            playerContext = new BarterPlayerContext(playerHero, playerParty.MobileParty);
            if (!TryBuildPeaceBarter(playerHero, playerParty, targetHero, targetParty, request.Terms, out var barterData, out reason))
            {
                Reject(peer, request, playerHero.Gold, reason);
                return;
            }

            var barterManager = BarterManager.Instance;
            if (barterManager == null)
            {
                Reject(peer, request, playerHero.Gold, "The peace offer is not acceptable.");
                return;
            }

            var offeredBarterables = barterData.GetOfferedBarterables();
            var offerValue = barterManager.GetOfferValueForFaction(barterData, targetHero.Clan);
            if (offerValue < -0.01f)
            {
                Reject(peer, request, playerHero.Gold, "The peace offer is not acceptable.");
                return;
            }

            MakePeaceAction.Apply(playerHero.MapFaction, targetHero.MapFaction);
            if (playerHero.MapFaction.FactionsAtWarWith?.Contains(targetHero.MapFaction) == true ||
                targetHero.MapFaction.FactionsAtWarWith?.Contains(playerHero.MapFaction) == true)
            {
                Reject(peer, request, playerHero.Gold, "The peace agreement could not be completed.");
                return;
            }

            foreach (var barterable in offeredBarterables)
            {
                if (!(barterable is PeaceBarterable))
                    barterable.Apply();
            }
            CampaignEventDispatcher.Instance.OnBarterAccepted(playerHero, targetHero, offeredBarterables);
            ApplyOverpayRelationBonus(playerHero, targetHero, MathF.Max(0f, offerValue));

            if ((PeaceConversationContext)request.Context == PeaceConversationContext.MapParty)
                ConversationPartyHold.EndEngagement(conversationPartyTracker, peer);
            FlushHeroGold(playerHero);
            FlushHeroGold(targetHero);
            network.Send(peer, new NetworkPeaceBarterResult(
                request.ContextId,
                true,
                playerHero.Gold,
                requestId: request.RequestId));
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Failed to apply an authoritative peace barter");
            Reject(peer, request, playerHero?.Gold ?? 0, "The server could not process the peace offer.");
        }
        finally
        {
            playerContext?.Dispose();
        }
    }

    private bool TryResolveContext(
        NetPeer peer,
        NetworkRequestPeaceBarter request,
        out Hero playerHero,
        out PartyBase playerParty,
        out Hero targetHero,
        out PartyBase targetParty,
        out string reason)
    {
        playerHero = null;
        playerParty = null;
        targetHero = null;
        targetParty = null;
        reason = null;

        if (string.IsNullOrEmpty(request.RequestId) ||
            !Enum.IsDefined(typeof(PeaceConversationContext), request.Context) ||
            !playerManager.TryGetPlayer(peer, out Player player) ||
            !objectManager.TryGetObject(player.HeroId, out playerHero) ||
            !objectManager.TryGetObject(player.MobilePartyId, out MobileParty playerMobileParty) ||
            !objectManager.TryGetObject(request.TargetHeroId, out targetHero))
        {
            reason = "The server could not identify the peace participants.";
            return false;
        }

        playerParty = playerMobileParty.Party;
        targetParty = targetHero.PartyBelongedTo?.Party;
        if (playerMobileParty?.IsActive != true ||
            playerMobileParty.LeaderHero != playerHero)
        {
            reason = "The encounter state changed before the peace barter completed.";
            return false;
        }

        var context = (PeaceConversationContext)request.Context;
        if (context == PeaceConversationContext.MapParty)
        {
            if (!objectManager.TryGetObject(request.ContextId, out PartyBase targetPartyBase) ||
                targetPartyBase.MobileParty?.IsActive != true ||
                targetPartyBase.LeaderHero != targetHero ||
                playerMobileParty.MapEvent != null ||
                targetPartyBase.MobileParty.MapEvent != null ||
                !objectManager.TryGetId(playerParty, out var playerPartyId) ||
                !conversationPartyTracker.TryGetEngagement(peer, out var engagement) ||
                engagement.PartyId != request.ContextId ||
                engagement.EngagerPartyId != playerPartyId)
            {
                reason = "The peace encounter is no longer active.";
                return false;
            }

            targetParty = targetPartyBase;
        }
        else
        {
            if (targetHero.CharacterObject == null ||
                !objectManager.TryGetId(targetHero.CharacterObject, out var characterId) ||
                !locationConversationTracker.TryGetEngagement(peer, out var npcKey) ||
                npcKey != LocationConversationTracker.ComposeKey(request.ContextId, characterId))
            {
                reason = "The peace conversation is no longer active.";
                return false;
            }
        }

        if (!CanNegotiatePeace(playerHero, targetHero, out reason))
            return false;

        return true;
    }

    private static bool CanNegotiatePeace(Hero playerHero, Hero targetHero, out string reason)
    {
        reason = null;
        var playerClan = playerHero?.Clan;
        var targetClan = targetHero?.Clan;
        var playerFaction = playerHero?.MapFaction;
        var targetFaction = targetHero?.MapFaction;

        if (playerClan == null || targetClan == null || playerFaction == null || targetFaction == null ||
            !FactionManager.IsAtWarAgainstFaction(playerFaction, targetFaction))
        {
            reason = "The factions are no longer eligible to negotiate peace.";
            return false;
        }

        if (targetHero.IsPrisoner ||
            targetClan.IsRebelClan ||
            targetClan.IsUnderMercenaryService ||
            (targetClan.IsMinorFaction && Campaign.Current.Models.DiplomacyModel.IsAtConstantWar(targetFaction, playerFaction)) ||
            (targetClan.Kingdom != null && playerClan.Kingdom != null))
        {
            reason = "That lord cannot negotiate this peace agreement.";
            return false;
        }

        return true;
    }

    private bool TryBuildPeaceBarter(
        Hero playerHero,
        PartyBase playerParty,
        Hero targetHero,
        PartyBase targetParty,
        PeaceBarterTerm[] terms,
        out BarterData barterData,
        out string reason)
    {
        var barterManager = BarterManager.Instance;
        barterData = null;
        reason = null;
        if (barterManager == null)
        {
            reason = "The server barter system is unavailable.";
            return false;
        }

        barterData = new BarterData(
            playerHero,
            targetHero,
            playerParty,
            targetParty,
            barterManager.InitializeMakePeaceBarterContext);

        var peaceBarterable = new PeaceBarterable(
            targetHero,
            playerHero.Clan.MapFaction,
            targetHero.MapFaction,
            CampaignTime.Years(1f));
        peaceBarterable.SetIsOffered(true);
        barterData.AddBarterable<OtherBarterGroup>(peaceBarterable, true);
        CampaignEventDispatcher.Instance.OnBarterablesRequested(barterData);

        return TryApplyRequestedTerms(
            playerHero,
            targetHero,
            barterData,
            terms ?? Array.Empty<PeaceBarterTerm>(),
            out reason);
    }

    private bool TryApplyRequestedTerms(
        Hero playerHero,
        Hero targetHero,
        BarterData barterData,
        PeaceBarterTerm[] terms,
        out string reason)
    {
        var usedBarterables = new HashSet<Barterable>();
        foreach (var term in terms)
        {
            if (!Enum.IsDefined(typeof(PeaceBarterTermType), term.Type) || term.Amount <= 0)
            {
                reason = "The peace offer contains an invalid term.";
                return false;
            }

            if (string.IsNullOrEmpty(term.OwnerHeroId))
            {
                reason = "The peace offer does not identify the owner of a barter term.";
                return false;
            }

            var termType = (PeaceBarterTermType)term.Type;
            var barterable = barterData.GetBarterables().FirstOrDefault(candidate =>
                (candidate.OriginalOwner == playerHero || candidate.OriginalOwner == targetHero) &&
                objectManager.TryGetId(candidate.OriginalOwner, out var ownerHeroId) &&
                ownerHeroId == term.OwnerHeroId &&
                MatchesTerm(candidate, termType, term));

            if (barterable == null || !usedBarterables.Add(barterable) || term.Amount > barterable.MaxAmount)
            {
                reason = "The peace offer no longer matches the server's available barter terms.";
                return false;
            }

            barterable.CurrentAmount = term.Amount;
            barterable.SetIsOffered(true);
        }

        reason = null;
        return true;
    }

    private bool MatchesTerm(Barterable barterable, PeaceBarterTermType type, PeaceBarterTerm term)
    {
        switch (type)
        {
            case PeaceBarterTermType.Gold:
                return barterable is GoldBarterable;
            case PeaceBarterTermType.Item:
                return barterable is ItemBarterable itemBarterable && MatchesItem(itemBarterable, term);
            case PeaceBarterTermType.Fief:
                return barterable is FiefBarterable fiefBarterable &&
                       objectManager.TryGetId(fiefBarterable.TargetSettlement, out var settlementId) &&
                       settlementId == term.ObjectId;
            case PeaceBarterTermType.TransferPrisoner:
                return barterable is TransferPrisonerBarterable prisonerBarterable &&
                       MatchesPrisoner(prisonerBarterable._prisonerCharacter, term);
            case PeaceBarterTermType.ReleasePrisoner:
                return barterable is SetPrisonerFreeBarterable releasedPrisoner &&
                       MatchesPrisoner(releasedPrisoner._prisonerCharacter, term);
            default:
                return false;
        }
    }

    private bool MatchesItem(ItemBarterable barterable, PeaceBarterTerm term)
    {
        var equipmentElement = barterable.ItemRosterElement.EquipmentElement;
        if (!objectManager.TryGetId(equipmentElement.Item, out var itemId) || itemId != term.ObjectId)
            return false;

        var modifier = equipmentElement.ItemModifier;
        if ((modifier == null) != term.ItemModifierNull) return false;
        if (modifier == null) return true;

        return objectManager.TryGetId(modifier, out var modifierId) && modifierId == term.ItemModifierId;
    }

    private bool MatchesPrisoner(Hero prisoner, PeaceBarterTerm term)
        => prisoner?.CharacterObject != null &&
           objectManager.TryGetId(prisoner.CharacterObject, out var prisonerId) &&
           prisonerId == term.ObjectId;

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

    private void Reject(NetPeer peer, NetworkRequestPeaceBarter request, int playerGold, string reason)
    {
        Logger.Warning("Rejected peace barter for {ContextId}: {Reason}", request.ContextId, reason);
        network.Send(peer, new NetworkPeaceBarterResult(
            request.ContextId,
            false,
            playerGold,
            reason,
            request.RequestId));
    }
}
