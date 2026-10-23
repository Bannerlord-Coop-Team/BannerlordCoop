using Common.Messaging;
using GameInterface.Registry.Auto;
using GameInterface.Services.Armies;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
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
    void RepairOrphanedMapEvents();
}

internal sealed class MapEventLoadCleaner : IMapEventLoadCleaner
{
    private static readonly AccessTools.FieldRef<MapEvent, TroopUpgradeTracker> TrackerField =
        AccessTools.FieldRefAccess<MapEvent, TroopUpgradeTracker>("<TroopUpgradeTracker>k__BackingField");

    private readonly ILogger logger;
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly IArmyDisbander armyDisbander;
    private readonly IOrphanedMapEventFinder orphanedMapEventFinder;

    public MapEventLoadCleaner(
        ILogger logger,
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        IArmyDisbander armyDisbander,
        IOrphanedMapEventFinder orphanedMapEventFinder)
    {
        this.logger = logger;
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.armyDisbander = armyDisbander;
        this.orphanedMapEventFinder = orphanedMapEventFinder;
    }

    public void FinalizePlayerMapEvents()
    {
        if (Campaign.Current?.MapEventManager == null)
            return;

        var partiesBefore = Campaign.Current.CampaignObjectManager.MobileParties.ToArray();
        try
        {
            foreach (var mapEvent in GetLoadedPlayerMapEvents())
            {
                FinalizePlayerMapEvent(mapEvent);
            }
        }
        finally
        {
            PublishDestroyedParties(partiesBefore);
        }
    }

    private void FinalizePlayerMapEvent(MapEvent mapEvent)
    {
        MobileParty[] involvedMobileParties;
        Army[] playerLedArmies;
        MobileParty[] releasedArmyParties;

        try
        {
            var (repairedLinks, removedEntries) = RepairPartySideLinks(mapEvent);
            if (repairedLinks > 0 || removedEntries > 0)
            {
                logger.Warning(
                    "Repaired {RepairedLinkCount} party-side links and removed {RemovedEntryCount} stale or duplicate participants from loaded player map event {MapEventId} before finalization",
                    repairedLinks,
                    removedEntries,
                    mapEvent.StringId);
            }

            involvedMobileParties = mapEvent.InvolvedParties
                .Where(party => party.IsMobile && party.MobileParty.IsActive)
                .Select(party => party.MobileParty)
                .ToArray();
            playerLedArmies = involvedMobileParties
                .Select(mobileParty => mobileParty.Army)
                .Where(army => army != null && army.LeaderParty.IsPlayerParty())
                .Distinct()
                .ToArray();
            releasedArmyParties = playerLedArmies
                .SelectMany(army => army.Parties)
                .Where(mobileParty => mobileParty.IsActive && !mobileParty.IsPlayerParty())
                .Distinct()
                .ToArray();
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to finalize loaded player map event {MapEventId}", mapEvent.StringId);
            return;
        }

        try
        {
            FinalizeOrRepairEvent(mapEvent);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to finalize loaded player map event {MapEventId}", mapEvent.StringId);
        }

        try
        {
            ReleaseParties(mapEvent, involvedMobileParties, playerLedArmies, releasedArmyParties);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to release parties of loaded player map event {MapEventId}", mapEvent.StringId);
        }
    }

    private void ReleaseParties(
        MapEvent mapEvent,
        MobileParty[] involvedMobileParties,
        Army[] playerLedArmies,
        MobileParty[] releasedArmyParties)
    {
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

    public void RepairOrphanedMapEvents()
    {
        if (Campaign.Current?.MapEventManager == null)
            return;

        var partiesBefore = Campaign.Current.CampaignObjectManager.MobileParties.ToArray();
        try
        {
            var listed = new HashSet<MapEvent>(Campaign.Current.MapEventManager.MapEvents);
            var parties = Campaign.Current.CampaignObjectManager.MobileParties
                .Select(mobileParty => mobileParty.Party)
                .Concat(Settlement.All.Select(settlement => settlement.Party))
                .ToList();
            var orphaned = orphanedMapEventFinder.Find(parties, listed);

            foreach (var mapEvent in orphaned.Finalized)
            {
                try
                {
                    RepairOrphanedMapEvent(mapEvent, orphaned.LinkedParties(mapEvent));
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to repair orphaned map event {MapEventId}", mapEvent.StringId);
                }
            }

            foreach (var mapEvent in orphaned.UnlistedLive)
            {
                logger.Warning(
                    "Map event {MapEventId} is referenced by {PartyCount} parties but is neither listed by the map event manager nor finalized, leaving it as is",
                    mapEvent.StringId,
                    orphaned.LinkedParties(mapEvent).Count);
            }
        }
        finally
        {
            PublishDestroyedParties(partiesBefore);
        }
    }

    private void RepairOrphanedMapEvent(MapEvent mapEvent, IReadOnlyList<PartyBase> linkedParties)
    {
        var candidates = linkedParties
            .Concat((mapEvent._sides ?? Array.Empty<MapEventSide>())
                .Where(side => side != null)
                .SelectMany(side => side.Parties)
                .Select(mapEventParty => mapEventParty?.Party)
                .Where(party => party != null))
            .Distinct()
            .ToArray();
        var componentName = mapEvent.Component?.GetType().Name;
        var battleState = mapEvent.BattleState;

        var (repairedLinks, removedEntries) = RepairPartySideLinks(mapEvent);
        ClearLinksWithoutEntry(mapEvent, candidates);
        DetachFinalizedEvent(mapEvent);

        foreach (var party in candidates)
        {
            if (ReferenceEquals(party._mapEventSide?._mapEvent, mapEvent))
                party._mapEventSide = null;
        }

        // A party that now belongs to another live event was not released from this one.
        var released = candidates.Where(party => party._mapEventSide == null).ToArray();
        var releasedMobileParties = released.Where(party => party.IsMobile).Select(party => party.MobileParty).ToArray();

        foreach (var mobileParty in releasedMobileParties)
        {
            if (mobileParty.IsActive && !mobileParty.IsPlayerParty())
                mobileParty.ResetNavigationToHold();
        }

        logger.Warning(
            "Released {PartyCount} parties from orphaned finalized map event {MapEventId} ({Component}, {BattleState}, repaired {RepairedLinkCount} links, removed {RemovedEntryCount} entries): {PartyIds}",
            released.Length,
            mapEvent.StringId,
            componentName,
            battleState,
            repairedLinks,
            removedEntries,
            string.Join(", ", released.Select(party => party.Id)));
    }

    // Releasing an army leader also releases its attached parties, and vanilla removal of a linked
    // party that has no entry in its side reads index -1.
    private static void ClearLinksWithoutEntry(MapEvent mapEvent, IEnumerable<PartyBase> parties)
    {
        foreach (var party in parties)
        {
            var side = party._mapEventSide;
            if (side == null || !ReferenceEquals(side._mapEvent, mapEvent))
                continue;

            if (!side._battleParties.Any(entry => ReferenceEquals(entry?.Party, party)))
                party._mapEventSide = null;
        }
    }

    // The load repairs run before the server state that lets lifetime patches publish, so a party that
    // vanilla destroyed during a pass (a participant or a cascade elsewhere) is never unregistered
    // otherwise. This reconciles every party that left the campaign during the pass.
    private void PublishDestroyedParties(IEnumerable<MobileParty> partiesBeforePass)
    {
        var campaignParties = new HashSet<MobileParty>(Campaign.Current.CampaignObjectManager.MobileParties);
        foreach (var party in partiesBeforePass)
        {
            if (campaignParties.Contains(party) || !objectManager.Contains(party))
                continue;

            logger.Warning("Unregistering party {PartyId} removed from the campaign during a load repair pass", party.StringId);
            messageBroker.Publish(party, new InstanceDestroyed<MobileParty>(party));
        }
    }

    private static void RestoreUnsavedState(MapEvent mapEvent)
    {
        mapEvent.Component?.AfterLoad(mapEvent);
        if (TrackerField(mapEvent) == null)
            TrackerField(mapEvent) = new TroopUpgradeTracker();
    }

    private (int RepairedLinks, int RemovedEntries) RepairPartySideLinks(MapEvent mapEvent)
    {
        var repairedLinks = 0;
        var removedEntries = 0;
        var sides = (mapEvent._sides ?? Array.Empty<MapEventSide>())
            .Where(side => side != null)
            .ToArray();
        var memberships = new Dictionary<PartyBase, List<MapEventSide>>();

        foreach (var side in sides)
        {
            foreach (var mapEventParty in side.Parties)
            {
                var party = mapEventParty?.Party;
                if (party == null)
                    continue;

                if (!memberships.TryGetValue(party, out var partySides))
                {
                    partySides = new List<MapEventSide>();
                    memberships.Add(party, partySides);
                }

                if (!partySides.Contains(side))
                    partySides.Add(side);
            }
        }

        var canonicalSides = new Dictionary<PartyBase, MapEventSide>();
        foreach (var membership in memberships)
        {
            var party = membership.Key;
            var linkedSide = party.MapEventSide;

            if (linkedSide != null && membership.Value.Contains(linkedSide))
            {
                canonicalSides.Add(party, linkedSide);
                continue;
            }

            // A link to another live map event is authoritative. Drop this loaded event's stale
            // membership instead of moving the party out of its current event.
            if (linkedSide != null && !sides.Contains(linkedSide))
                continue;

            canonicalSides.Add(party, membership.Value[0]);
        }

        var retainedParties = new HashSet<PartyBase>();
        foreach (var side in sides)
        {
            for (var i = side._battleParties.Count - 1; i >= 0; i--)
            {
                var party = side._battleParties[i]?.Party;
                if (party != null &&
                    canonicalSides.TryGetValue(party, out var canonicalSide) &&
                    ReferenceEquals(canonicalSide, side) &&
                    retainedParties.Add(party))
                {
                    continue;
                }

                side._battleParties.RemoveAt(i);
                removedEntries++;
            }

            if (side._battleParties.Count > 0 &&
                !side._battleParties.Any(entry => ReferenceEquals(entry?.Party, side.LeaderParty)))
            {
                side.LeaderParty = side._battleParties[0].Party;
                side._mapFaction = side.LeaderParty.MapFaction;
            }
        }

        foreach (var membership in canonicalSides)
        {
            if (ReferenceEquals(membership.Key.MapEventSide, membership.Value))
                continue;

            membership.Key._mapEventSide = membership.Value;
            repairedLinks++;
        }

        return (repairedLinks, removedEntries);
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
        DetachFinalizedEvent(mapEvent);
    }

    private void DetachFinalizedEvent(MapEvent mapEvent)
    {
        // The load restores unsaved state only for events the manager lists. Without it the component's
        // MapEvent back-reference is null, which FieldBattleEventComponent reads on every party add and
        // remove, so even a one-party side fails; the position lists and tracker are null as well.
        if (!Campaign.Current.MapEventManager.MapEvents.Contains(mapEvent))
            RestoreUnsavedState(mapEvent);

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
