using Common.Messaging;
using GameInterface.Registry.Auto;
using GameInterface.Services.Armies;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MapEvents;

public interface IMapEventLoadCleaner
{
    void FinalizePlayerMapEvents();
}

internal sealed class MapEventLoadCleaner : IMapEventLoadCleaner
{
    private readonly ILogger logger;
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly IArmyDisbander armyDisbander;

    public MapEventLoadCleaner(
        ILogger logger,
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        IArmyDisbander armyDisbander)
    {
        this.logger = logger;
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.armyDisbander = armyDisbander;
    }

    public void FinalizePlayerMapEvents()
    {
        if (Campaign.Current?.MapEventManager == null)
            return;

        var loadedPlayerMapEvents = GetLoadedPlayerMapEvents();

        foreach (var mapEvent in loadedPlayerMapEvents)
        {
            RepairPartySideLinks(mapEvent);

            var involvedMobileParties = mapEvent.InvolvedParties
                .Where(party => party.IsMobile && party.MobileParty.IsActive)
                .Select(party => party.MobileParty)
                .ToArray();
            var playerLedArmies = involvedMobileParties
                .Select(mobileParty => mobileParty.Army)
                .Where(army => army != null && army.LeaderParty.IsPlayerParty())
                .Distinct()
                .ToArray();
            var releasedArmyParties = playerLedArmies
                .SelectMany(army => army.Parties)
                .Where(mobileParty => mobileParty.IsActive && !mobileParty.IsPlayerParty())
                .Distinct()
                .ToArray();

            FinalizeOrRepairEvent(mapEvent);

            foreach (var army in playerLedArmies)
            {
                logger.Information(
                    "Dispersing loaded player-led army {ArmyName} released from map event {MapEventId}",
                    army.Name,
                    mapEvent.StringId);
                armyDisbander.Disband(army, Army.ArmyDispersionReason.Unknown);
            }

            foreach (var mobileParty in involvedMobileParties.Concat(releasedArmyParties).Distinct())
            {
                if (!mobileParty.IsActive || mobileParty.IsPlayerParty())
                    continue;

                if (releasedArmyParties.Contains(mobileParty) &&
                    TrySetReleasedPartySettlementObjective(mobileParty, out var settlement))
                {
                    logger.Information(
                        "Sending released army party {PartyId} to {SettlementId} after finalizing map event {MapEventId}",
                        mobileParty.StringId,
                        settlement.StringId,
                        mapEvent.StringId);
                    continue;
                }

                mobileParty.ResetNavigationToHold();
            }
        }
    }

    private void RepairPartySideLinks(MapEvent mapEvent)
    {
        var repairedLinks = 0;

        foreach (var side in mapEvent._sides ?? Array.Empty<MapEventSide>())
        {
            if (side?.Parties == null)
                continue;

            foreach (var mapEventParty in side.Parties)
            {
                var party = mapEventParty?.Party;
                if (party == null || party.MapEventSide != null)
                    continue;

                // HandleMapEventEnd loops until side.Parties is empty and relies on the PartyBase
                // setter to remove the current entry. A null backing link makes that setter a no-op,
                // leaving the vanilla loop permanently stuck. Do not rewrite a non-null link because
                // it may authoritatively belong to another event.
                party._mapEventSide = side;
                repairedLinks++;
            }
        }

        if (repairedLinks > 0)
        {
            logger.Warning(
                "Repaired {RepairedLinkCount} missing party-side links in loaded player map event {MapEventId} before finalization",
                repairedLinks,
                mapEvent.StringId);
        }
    }

    private void FinalizeOrRepairEvent(MapEvent mapEvent)
    {
        if (!mapEvent.IsFinalized)
        {
            logger.Information(
                "Finalizing loaded player map event {MapEventId} with {PartyCount} involved parties",
                mapEvent.StringId,
                mapEvent.InvolvedParties.Count());
            mapEvent.FinalizeEvent();
            return;
        }

        logger.Warning(
            "Detaching {PartyCount} parties from half-finalized loaded player map event {MapEventId}",
            mapEvent.InvolvedParties.Count(),
            mapEvent.StringId);
        mapEvent.AttackerSide?.HandleMapEventEnd();
        mapEvent.DefenderSide?.HandleMapEventEnd();
        messageBroker.Publish(mapEvent, new MapEventFinalized(mapEvent));

        if (objectManager.Contains(mapEvent))
            messageBroker.Publish(mapEvent, new InstanceDestroyed<MapEvent>(mapEvent));
    }

    private MapEvent[] GetLoadedPlayerMapEvents()
    {
        var mapEvents = Campaign.Current.MapEventManager.MapEvents
            .Where(mapEvent => !mapEvent.IsFinalized && mapEvent.ContainsPlayerParty())
            .ToList();

        foreach (var player in playerManager.Players)
        {
            if (!TryGetSavedPlayerParty(player.MobilePartyId, out var party))
                continue;

            var mapEvent = party.MapEvent;
            if (mapEvent == null ||
                mapEvents.Any(candidate => ReferenceEquals(candidate, mapEvent)))
            {
                continue;
            }

            mapEvents.Add(mapEvent);
        }

        return mapEvents.ToArray();
    }

    private bool TryGetSavedPlayerParty(string partyId, out MobileParty party)
    {
        if (objectManager.TryGetObject(partyId, out party))
            return true;

        var partyStringId = global::GameInterface.Services.ObjectManager.ObjectManager.Compact(
            partyId,
            typeof(MobileParty));
        party = string.IsNullOrEmpty(partyStringId)
            ? null
            : Campaign.Current.CampaignObjectManager.MobileParties
                .FirstOrDefault(candidate => candidate.StringId == partyStringId);
        if (party == null)
            return objectManager.TryGetObjectWithLogging(partyId, out party);

        logger.Warning(
            "Resolved saved player party {PartyId} from CampaignObjectManager because it was missing from the network object manager",
            partyId);
        return true;
    }

    private static bool TrySetReleasedPartySettlementObjective(
        MobileParty mobileParty,
        out Settlement settlement)
    {
        var navigationType = mobileParty.IsCurrentlyAtSea
            ? MobileParty.NavigationType.Naval
            : MobileParty.NavigationType.Default;

        settlement = FindDestination(mobileParty, navigationType, requireFriendly: true) ??
            FindDestination(mobileParty, navigationType, requireFriendly: false);
        if (settlement == null)
            return false;

        SetPartyAiAction.GetActionForVisitingSettlement(
            mobileParty,
            settlement,
            navigationType,
            isFromPort: false,
            isTargetingPort: mobileParty.IsCurrentlyAtSea);
        return true;
    }

    private static Settlement FindDestination(
        MobileParty mobileParty,
        MobileParty.NavigationType navigationType,
        bool requireFriendly)
    {
        bool IsEligible(Settlement candidate) =>
            IsEligibleDestination(mobileParty, candidate, requireFriendly);

        if (IsEligible(mobileParty.HomeSettlement))
            return mobileParty.HomeSettlement;

        return SettlementHelper.FindNearestSettlementToMobileParty(
                mobileParty,
                navigationType,
                IsEligible) ??
            SettlementHelper.FindNearestSettlementToPoint(mobileParty.Position, IsEligible);
    }

    private static bool IsEligibleDestination(
        MobileParty mobileParty,
        Settlement settlement,
        bool requireFriendly)
    {
        if (settlement == null ||
            (!settlement.IsFortification && !settlement.IsVillage) ||
            settlement.IsUnderSiege ||
            settlement.IsUnderRaid ||
            (mobileParty.IsCurrentlyAtSea && !settlement.HasPort))
        {
            return false;
        }

        return !requireFriendly ||
            mobileParty.MapFaction == null ||
            settlement.MapFaction == null ||
            !FactionManager.IsAtWarAgainstFaction(mobileParty.MapFaction, settlement.MapFaction);
    }
}
