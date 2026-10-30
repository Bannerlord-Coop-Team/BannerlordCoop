using Common;
using Common.Logging;
using Common.Network;
using GameInterface.CoopSessionData;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Romance = TaleWorlds.CampaignSystem.Romance;

namespace GameInterface.Services.Heroes.Interfaces;

public interface IMarriageOfferCampaignBehaviorInterface : IGameAbstraction
{
    void DailyTickClan(Clan consideringClan);
    void HourlyTick();
    void AcceptMarriageOffer(string playerHeroId, string playerClanHeroId, string otherClanHeroId);
    void DeclineMarriageOffer(string playerHeroId, string playerClanHeroId, string otherClanHeroId);
    void OnHeroPrisonerTaken(Hero prisoner);
    void OnHeroesMarried(Hero hero1, Hero hero2);
    void OnHeroKilled(Hero victim);
    void OnArmyCreated();
    void OnMapEventStarted();
    void CharacterBecameFugitive();
    void OnWarDeclared();
    void OnHeroRelationChanged(Hero effectiveHero, Hero effectiveHeroGainedRelationWith);
    void OnClanChangedKingdom(Clan clan);
    void AddNewPlayerHeroId(string playerHeroId, NetPeer peer);
}

public class MarriageOfferCampaignBehaviorInterface : IMarriageOfferCampaignBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<MarriageOfferCampaignBehaviorInterface>();

    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    private Dictionary<string, MarriageOfferData> PlayerMarriageOffers => coopSessionProvider.CoopSession.AgingPlayerData.PlayerMarriageOffers;

    private static MarriageModel MarriageModel => Campaign.Current.Models.MarriageModel;

    public MarriageOfferCampaignBehaviorInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        INetwork network)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.network = network;
    }

    public void DailyTickClan(Clan consideringClan)
    {
        if (!TryGetMarriageOfferBehavior(out var marriageOfferBehavior)) return;

        // Only NPC clans send offers, marrying into another player's clan needs that player's consent
        if (consideringClan.IsPlayerClan() || !MarriageModel.IsClanSuitableForMarriage(consideringClan)) return;

        MobileParty.NavigationType navigationType = consideringClan.HasNavalNavigationCapability ? MobileParty.NavigationType.All : MobileParty.NavigationType.Default;

        foreach (var playerOffer in PlayerMarriageOffers)
        {
            MarriageOfferData offer = playerOffer.Value;
            if (!objectManager.TryGetObjectWithLogging<Hero>(playerOffer.Key, out var playerHero)) continue;

            // Offers go to the clan leader since they decide on the clan's marriages
            Clan playerClan = playerHero.Clan;
            if (playerClan?.Leader != playerHero) continue;

            if (offer.CurrentOfferedPlayerClanHeroId != null ||
                new CampaignTime(offer.LastMarriageOfferTimeNumTicks).ElapsedDaysUntilNow < MarriageOfferCampaignBehavior.MarriageOfferCooldownDurationAsDays ||
                playerHero.IsPrisoner ||
                playerHero.PartyBelongedTo?.IsInRaftState != false ||
                !MarriageModel.ShouldNpcMarriageBetweenClansBeAllowed(playerClan, consideringClan)) continue;

            float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(playerClan.FactionMidSettlement, consideringClan.FactionMidSettlement, false, false, navigationType);
            if (MBRandom.RandomFloat < (distance / Campaign.Current.Models.MapDistanceModel.GetMaximumDistanceBetweenTwoConnectedSettlements(navigationType)) - 0.5f) continue;

            foreach (Hero hero in playerClan.Heroes)
            {
                if (hero != playerHero && !hero.IsPlayerHero() && hero.CanMarry() &&
                    !marriageOfferBehavior._acceptedMarriageOffersThatWaitingForAvailability.ContainsKey(hero) &&
                    ConsiderMarriageForPlayerClanMember(playerOffer.Key, offer, hero, consideringClan))
                {
                    break;
                }
            }
        }
    }

    public void HourlyTick()
    {
        if (!TryGetMarriageOfferBehavior(out var marriageOfferBehavior)) return;

        foreach (var playerOffer in PlayerMarriageOffers)
        {
            MarriageOfferData offer = playerOffer.Value;
            if (offer.CurrentOfferedPlayerClanHeroId != null &&
                new CampaignTime(offer.LastMarriageOfferTimeNumTicks).ElapsedDaysUntilNow >= MarriageOfferCampaignBehavior.MapNotificationAutoDeclineDurationInDays)
            {
                CancelMarriageOffer(playerOffer.Key, offer);
            }
        }

        var waitingOffers = marriageOfferBehavior._acceptedMarriageOffersThatWaitingForAvailability;
        foreach (var waitingOffer in waitingOffers.ToList())
        {
            Hero playerClanHero = waitingOffer.Key;
            Hero otherClanHero = waitingOffer.Value;

            // Deliberate change from vanilla, which never marries a waiting couple because being engaged fails the suitability check, so they stay engaged forever.
            // Leaving them out while checking lets the "marriage preparations started" accept finish once both heroes are available.
            waitingOffers.Remove(playerClanHero);
            bool canMarry = MarriageModel.IsCoupleSuitableForMarriage(playerClanHero, otherClanHero);
            waitingOffers[playerClanHero] = otherClanHero;

            if (canMarry)
            {
                RemoveEngagement(waitingOffers, playerClanHero);
                MarryHeroesViaOffer(playerClanHero, otherClanHero);
            }
            else if (!MarriageModel.ShouldNpcMarriageBetweenClansBeAllowed(playerClanHero.Clan, otherClanHero.Clan))
            {
                RemoveEngagement(waitingOffers, playerClanHero);
            }
        }
    }

    public void AcceptMarriageOffer(string playerHeroId, string playerClanHeroId, string otherClanHeroId)
    {
        if (!TryGetMarriageOfferBehavior(out var marriageOfferBehavior)) return;
        if (!TryGetActiveOffer(playerHeroId, out var offer, out var playerClanHero, out var otherClanHero)) return;

        // The offer the player answered expired or was replaced before the response arrived
        if (offer.CurrentOfferedPlayerClanHeroId != playerClanHeroId || offer.CurrentOfferedOtherClanHeroId != otherClanHeroId) return;

        // Clear the offer first, otherwise the marriage below cancels it and closes it on the client a second time
        FinalizeMarriageOffer(offer);

        bool married = MarriageModel.IsCoupleSuitableForMarriage(playerClanHero, otherClanHero);
        if (married)
        {
            MarryHeroesViaOffer(playerClanHero, otherClanHero);
        }
        else
        {
            marriageOfferBehavior._acceptedMarriageOffersThatWaitingForAvailability[playerClanHero] = otherClanHero;

            // Engaged heroes can't marry anyone else, so drop other players' offers for either of them
            CancelOffersWhere((playerHero, offeredPlayerClanHero, offeredOtherClanHero) =>
                offeredPlayerClanHero == playerClanHero || offeredPlayerClanHero == otherClanHero ||
                offeredOtherClanHero == playerClanHero || offeredOtherClanHero == otherClanHero);
        }

        network.SendAll(new NetworkMarriageOfferAcceptResolved(playerHeroId, playerClanHeroId, otherClanHeroId, married));
    }

    public void DeclineMarriageOffer(string playerHeroId, string playerClanHeroId, string otherClanHeroId)
    {
        if (!PlayerMarriageOffers.TryGetValue(playerHeroId, out var offer)) return;

        // Skip a decline for an offer that expired or was replaced before the response arrived
        if (offer.CurrentOfferedPlayerClanHeroId != playerClanHeroId || offer.CurrentOfferedOtherClanHeroId != otherClanHeroId) return;

        // Vanilla declines through the cancel event, which also clears the offer on the client
        CancelMarriageOffer(playerHeroId, offer);
    }

    public void OnHeroPrisonerTaken(Hero prisoner)
    {
        CancelOffersWhere((playerHero, playerClanHero, otherClanHero) =>
            prisoner == playerHero || prisoner == playerClanHero || prisoner == otherClanHero);
    }

    public void OnHeroesMarried(Hero hero1, Hero hero2)
    {
        // Vanilla only checks the offered couple marrying each other, but with offers for several players either hero can also marry through another offer
        CancelOffersWhere((playerHero, playerClanHero, otherClanHero) =>
            hero1 == playerClanHero || hero1 == otherClanHero || hero2 == playerClanHero || hero2 == otherClanHero);
    }

    public void OnHeroKilled(Hero victim)
    {
        CancelOffersWhere((playerHero, playerClanHero, otherClanHero) =>
            victim == playerHero || victim == playerClanHero || victim == otherClanHero);

        if (!TryGetMarriageOfferBehavior(out var marriageOfferBehavior)) return;

        var waitingOffers = marriageOfferBehavior._acceptedMarriageOffersThatWaitingForAvailability;
        Hero waitingPlayerClanHero = null;
        foreach (var waitingOffer in waitingOffers)
        {
            if (waitingOffer.Key == victim || waitingOffer.Value == victim)
            {
                waitingPlayerClanHero = waitingOffer.Key;
            }
        }
        if (waitingPlayerClanHero == null) return;

        Hero waitingOtherClanHero = waitingOffers[waitingPlayerClanHero];
        RemoveEngagement(waitingOffers, waitingPlayerClanHero);

        Hero deadHero = waitingPlayerClanHero.IsDead ? waitingPlayerClanHero : waitingOtherClanHero.IsDead ? waitingOtherClanHero : null;
        Hero otherHero = deadHero == waitingPlayerClanHero ? waitingOtherClanHero : waitingPlayerClanHero;
        Hero playerHero = waitingPlayerClanHero.Clan?.Leader;
        if (deadHero == null || playerHero == null) return;

        if (!objectManager.TryGetIdWithLogging(playerHero, out var playerHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(deadHero, out var deadHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(otherHero, out var otherHeroId)) return;

        network.SendAll(new NetworkMarriagePreparationCanceled(playerHeroId, deadHeroId, otherHeroId));
    }

    public void OnArmyCreated()
    {
        CancelOffersWhere((playerHero, playerClanHero, otherClanHero) =>
            playerClanHero.PartyBelongedTo?.Army != null || otherClanHero.PartyBelongedTo?.Army != null);
    }

    public void OnMapEventStarted()
    {
        CancelOffersWhere((playerHero, playerClanHero, otherClanHero) =>
            playerClanHero.PartyBelongedTo?.MapEvent != null || otherClanHero.PartyBelongedTo?.MapEvent != null);
    }

    public void CharacterBecameFugitive()
    {
        CancelOffersWhere((playerHero, playerClanHero, otherClanHero) =>
            !playerClanHero.IsActive || !otherClanHero.IsActive);
    }

    public void OnWarDeclared()
    {
        CancelOffersWhere((playerHero, playerClanHero, otherClanHero) =>
            !MarriageModel.IsCoupleSuitableForMarriage(playerClanHero, otherClanHero) ||
            !MarriageModel.ShouldNpcMarriageBetweenClansBeAllowed(playerHero.Clan, otherClanHero.Clan));
    }

    public void OnHeroRelationChanged(Hero effectiveHero, Hero effectiveHeroGainedRelationWith)
    {
        CancelOffersWhere((playerHero, playerClanHero, otherClanHero) =>
            (effectiveHero.Clan == playerClanHero.Clan || effectiveHero.Clan == otherClanHero.Clan) &&
            (effectiveHeroGainedRelationWith.Clan == playerClanHero.Clan || effectiveHeroGainedRelationWith.Clan == otherClanHero.Clan) &&
            !MarriageModel.ShouldNpcMarriageBetweenClansBeAllowed(playerClanHero.Clan, otherClanHero.Clan));
    }

    public void OnClanChangedKingdom(Clan clan)
    {
        CancelOffersWhere((playerHero, playerClanHero, otherClanHero) =>
            (playerClanHero.Clan == clan || otherClanHero.Clan == clan) &&
            !MarriageModel.ShouldNpcMarriageBetweenClansBeAllowed(playerClanHero.Clan, otherClanHero.Clan));
    }

    public void AddNewPlayerHeroId(string playerHeroId, NetPeer peer)
    {
        if (PlayerMarriageOffers.TryGetValue(playerHeroId, out var offer))
        {
            // The offer notification only lives on the client, so a rejoining player needs a pending offer sent again
            if (ModInformation.IsServer && peer != null && offer.CurrentOfferedPlayerClanHeroId != null)
            {
                network.Send(peer, new NetworkMarriageOfferSent(playerHeroId, offer.CurrentOfferedPlayerClanHeroId, offer.CurrentOfferedOtherClanHeroId));
            }
            return;
        }

        PlayerMarriageOffers[playerHeroId] = new(null, null, 0);
    }

    private bool ConsiderMarriageForPlayerClanMember(string playerHeroId, MarriageOfferData offer, Hero playerClanHero, Clan consideringClan)
    {
        foreach (Hero hero in consideringClan.Heroes)
        {
            float marriageChance = MarriageModel.NpcCoupleMarriageChance(playerClanHero, hero);
            if (marriageChance <= 0f || MBRandom.RandomFloat >= marriageChance) continue;

            foreach (Romance.RomanticState romanticState in Romance.RomanticStateList)
            {
                if (romanticState.Level >= Romance.RomanceLevelEnum.MatchMadeByFamily &&
                    (romanticState.Person1 == playerClanHero || romanticState.Person2 == playerClanHero || romanticState.Person1 == hero || romanticState.Person2 == hero))
                {
                    return false;
                }
            }

            CreateMarriageOffer(playerHeroId, offer, playerClanHero, hero);
            return true;
        }

        return false;
    }

    private void CreateMarriageOffer(string playerHeroId, MarriageOfferData offer, Hero playerClanHero, Hero otherClanHero)
    {
        if (!objectManager.TryGetIdWithLogging(playerClanHero, out var playerClanHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(otherClanHero, out var otherClanHeroId)) return;

        offer.CurrentOfferedPlayerClanHeroId = playerClanHeroId;
        offer.CurrentOfferedOtherClanHeroId = otherClanHeroId;
        offer.LastMarriageOfferTimeNumTicks = CampaignTime.Now.NumTicks;

        network.SendAll(new NetworkMarriageOfferSent(playerHeroId, playerClanHeroId, otherClanHeroId));
    }

    private void MarryHeroesViaOffer(Hero playerClanHero, Hero otherClanHero)
    {
        // Vanilla gives this relation to the main hero, so give it to the player leading the offered hero's clan
        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(playerClanHero.Clan.Leader, otherClanHero.Clan.Leader, MarriageOfferCampaignBehavior.OfferRelationGainAmountWithTheMarriageClan);
        MarriageAction.Apply(playerClanHero, otherClanHero);
    }

    private void RemoveEngagement(Dictionary<Hero, Hero> waitingOffers, Hero playerClanHero)
    {
        waitingOffers.Remove(playerClanHero);

        // Clients need the engagement list too since the marriage model checks it in dialogue
        if (!objectManager.TryGetIdWithLogging(playerClanHero, out var playerClanHeroId)) return;

        network.SendAll(new NetworkMarriageEngagementEnded(playerClanHeroId));
    }

    private void CancelOffersWhere(Func<Hero, Hero, Hero, bool> shouldCancel)
    {
        foreach (var playerOffer in PlayerMarriageOffers)
        {
            if (!TryGetActiveOffer(playerOffer.Key, out var offer, out var playerClanHero, out var otherClanHero)) continue;
            if (!objectManager.TryGetObjectWithLogging<Hero>(playerOffer.Key, out var playerHero)) continue;

            if (shouldCancel(playerHero, playerClanHero, otherClanHero))
            {
                CancelMarriageOffer(playerOffer.Key, offer);
            }
        }
    }

    private void CancelMarriageOffer(string playerHeroId, MarriageOfferData offer)
    {
        network.SendAll(new NetworkMarriageOfferCanceled(playerHeroId, offer.CurrentOfferedPlayerClanHeroId, offer.CurrentOfferedOtherClanHeroId));

        FinalizeMarriageOffer(offer);
    }

    private static void FinalizeMarriageOffer(MarriageOfferData offer)
    {
        offer.CurrentOfferedPlayerClanHeroId = null;
        offer.CurrentOfferedOtherClanHeroId = null;
    }

    private bool TryGetActiveOffer(string playerHeroId, out MarriageOfferData offer, out Hero playerClanHero, out Hero otherClanHero)
    {
        playerClanHero = null;
        otherClanHero = null;

        if (!PlayerMarriageOffers.TryGetValue(playerHeroId, out offer) || offer.CurrentOfferedPlayerClanHeroId == null) return false;
        if (!objectManager.TryGetObjectWithLogging(offer.CurrentOfferedPlayerClanHeroId, out playerClanHero)) return false;

        return objectManager.TryGetObjectWithLogging(offer.CurrentOfferedOtherClanHeroId, out otherClanHero);
    }

    private bool TryGetMarriageOfferBehavior(out MarriageOfferCampaignBehavior marriageOfferBehavior)
    {
        marriageOfferBehavior = Campaign.Current?.GetCampaignBehavior<MarriageOfferCampaignBehavior>();
        if (marriageOfferBehavior != null) return true;

        Logger.Debug("Skipping marriage offer update because the campaign behavior is unavailable");
        return false;
    }
}
