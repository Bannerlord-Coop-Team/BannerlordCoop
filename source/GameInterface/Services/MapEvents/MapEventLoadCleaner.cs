using Common.Messaging;
using GameInterface.Registry.Auto;
using GameInterface.Registry;
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
    // Finalizes loaded player battles and releases every party still linked to a battle that already ended.
    void CleanUpLoadedMapEvents();

    // Releases every party still linked to a battle that already ended, calling onReleased for each one
    // as it is released. A throw from onReleased is logged and the rest are still released. Also run live by coop.unstuck.
    PartyBase[] ReleaseEndedMapEventLinks(Action<PartyBase> onReleased);

    // Releases the parties of a battle whose finalize threw after it already ended, and publishes its end for the clients.
    void ReleaseFailedFinalization(MapEvent mapEvent);
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
    private readonly IStalledMapEventResolver stalledMapEventResolver;
    private readonly IOrphanedMapEventFinder orphanedMapEventFinder;
    private readonly ILoadRepairRegistryReconciler registryReconciler;

    public MapEventLoadCleaner(
        ILogger logger,
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        IArmyDisbander armyDisbander,
        IStalledMapEventResolver stalledMapEventResolver,
        IOrphanedMapEventFinder orphanedMapEventFinder,
        ILoadRepairRegistryReconciler registryReconciler)
    {
        this.logger = logger;
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.armyDisbander = armyDisbander;
        this.stalledMapEventResolver = stalledMapEventResolver;
        this.orphanedMapEventFinder = orphanedMapEventFinder;
        this.registryReconciler = registryReconciler;
    }

    public void CleanUpLoadedMapEvents()
    {
        if (Campaign.Current?.MapEventManager == null)
            return;

        var before = registryReconciler.Capture();
        try
        {
            foreach (var mapEvent in GetLoadedPlayerMapEvents())
            {
                CleanUpLoadedPlayerMapEvent(mapEvent);
            }

            // A save can hold an AI battle left with no healthy troops on one side, which nothing ends on its own.
            stalledMapEventResolver.ResolveStalledMapEvents();
            ReleaseEndedMapEventLinks(_ => { });
        }
        finally
        {
            registryReconciler.UnregisterRemoved(before);
        }
    }

    // Each stage is caught on its own, so one broken battle cannot stop the server from starting.
    private void CleanUpLoadedPlayerMapEvent(MapEvent mapEvent)
    {
        MobileParty[] involvedMobileParties;
        Army[] playerLedArmies;
        MobileParty[] releasedArmyParties;

        try
        {
            RepairPartySideLinks(mapEvent);

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
            logger.Error(e, "Failed to repair party links of loaded player map event {MapEventId}; skipping its finalize and party release", mapEvent.StringId);
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
            ReleasePlayerMapEventParties(mapEvent, involvedMobileParties, playerLedArmies, releasedArmyParties);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to release parties of loaded player map event {MapEventId}", mapEvent.StringId);
        }
    }

    private void ReleasePlayerMapEventParties(
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

    // A save can hold parties still linked to a battle that already ended (finalized, or no longer
    // in the map event manager). Nothing ends that battle again, so the party never moves again and
    // the map shows an empty 0/0 battle. Covers AI parties too, not only player battles.
    public PartyBase[] ReleaseEndedMapEventLinks(Action<PartyBase> onReleased)
    {
        var releasedParties = new List<PartyBase>();
        var liveMapEvents = new HashSet<MapEvent>(Campaign.Current.MapEventManager.MapEvents);
        var orphaned = orphanedMapEventFinder.Find(GetLinkedParties(), liveMapEvents);

        foreach (var mapEvent in orphaned.Finalized.Concat(orphaned.UnlistedLive))
        {
            try
            {
                // The battle already ended, so mark it finalized before detaching. Removing the last
                // party then cannot run FinalizeEvent again and replay its results.
                if (!mapEvent.IsFinalized)
                {
                    logger.Warning(
                        "Map event {MapEventId} is linked to {PartyCount} parties but is neither listed by the map event manager nor finalized, ending it",
                        mapEvent.StringId,
                        orphaned.LinkedParties(mapEvent).Count);
                    ResetVillageState(mapEvent);
                    mapEvent.State = MapEventState.WaitingRemoval;
                }

                releasedParties.AddRange(ReleaseLinkedParties(mapEvent, orphaned.LinkedParties(mapEvent), onReleased));
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to release the parties of ended map event {MapEventId}", mapEvent.StringId);
            }
        }

        return releasedParties.ToArray();
    }

    // FinalizeEventAux sets WaitingRemoval before its listeners, the component's OnFinalize and the party
    // detach, so none of those ran. Redo the village state and the detach, without replaying results.
    public void ReleaseFailedFinalization(MapEvent mapEvent)
    {
        ResetVillageState(mapEvent);

        var linkedParties = GetLinkedParties()
            .Where(party => ReferenceEquals(party.MapEventSide.MapEvent, mapEvent))
            .ToArray();
        ReleaseLinkedParties(mapEvent, linkedParties, _ => { });
    }

    private static PartyBase[] GetLinkedParties() =>
        Campaign.Current.CampaignObjectManager.MobileParties
            .Where(mobileParty => mobileParty != null)
            .Select(mobileParty => mobileParty.Party)
            .Concat(Settlement.All
                .Where(settlement => settlement != null)
                .Select(settlement => settlement.Party))
            .Where(party => party?.MapEventSide?.MapEvent != null)
            .ToArray();

    private PartyBase[] ReleaseLinkedParties(MapEvent mapEvent, IReadOnlyList<PartyBase> linkedParties, Action<PartyBase> onReleased)
    {
        // The event's sides can list a party that holds no link to it; detaching releases that party as well.
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
        DetachFromFinalizedEvent(mapEvent);

        foreach (var party in candidates)
        {
            // A link the event's sides never listed has no membership left to remove.
            if (ReferenceEquals(party.MapEventSide?.MapEvent, mapEvent))
                party._mapEventSide = null;
        }

        // A party that now belongs to another live event was not released from this one.
        var released = candidates.Where(party => party.MapEventSide == null).ToArray();

        logger.Warning(
            "Released {PartyCount} parties from ended map event {MapEventId} ({Component}, {BattleState}, repaired {RepairedLinkCount} links, removed {RemovedEntryCount} entries): {PartyIds}",
            released.Length,
            mapEvent.StringId,
            componentName,
            battleState,
            repairedLinks,
            removedEntries,
            string.Join(", ", released.Select(party => party.Id)));

        foreach (var party in released)
        {
            var mobileParty = party.MobileParty;
            if (mobileParty != null && mobileParty.IsActive && !mobileParty.IsPlayerParty())
                mobileParty.ResetNavigationToHold();

            // A failed broadcast must not leave the rest of the parties linked to the ended battle.
            try
            {
                onReleased(party);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Could not report released party {PartyId} from ended map event {MapEventId}", party.Id, mapEvent.StringId);
            }
        }

        return released;
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

    // WaitingRemoval skips the component's OnBeforeFinalize and OnFinalize, the only calls that move
    // the village off the state its OnInitialize set (raid in OnBeforeFinalize, force supplies and
    // force volunteers in OnFinalize). Apply only that village state change, not the completed events.
    private static void ResetVillageState(MapEvent mapEvent)
    {
        var settlement = mapEvent.MapEventSettlement;
        var villageState = settlement?.Village?.VillageState;

        if (mapEvent.IsRaid && villageState == Village.VillageStates.BeingRaided)
        {
            if (settlement.SettlementHitPoints <= 1E-05f)
                ChangeVillageStateAction.ApplyBySettingToLooted(settlement, mapEvent.AttackerSide?.LeaderParty?.MobileParty);
            else
                ChangeVillageStateAction.ApplyBySettingToNormal(settlement);
        }
        else if ((mapEvent.IsForcingSupplies && villageState == Village.VillageStates.ForcedForSupplies) ||
            (mapEvent.IsForcingVolunteers && villageState == Village.VillageStates.ForcedForVolunteers))
        {
            ChangeVillageStateAction.ApplyBySettingToNormal(settlement);
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

        if (repairedLinks > 0 || removedEntries > 0)
        {
            logger.Warning(
                "Repaired {RepairedLinkCount} party-side links and removed {RemovedEntryCount} stale or duplicate participants from loaded map event {MapEventId} before finalization",
                repairedLinks,
                removedEntries,
                mapEvent.StringId);
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
        DetachFromFinalizedEvent(mapEvent);
    }

    // Empties the sides of an event that is already finalized and retires its network object,
    // without running FinalizeEvent (and its battle results) again.
    private void DetachFromFinalizedEvent(MapEvent mapEvent)
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
