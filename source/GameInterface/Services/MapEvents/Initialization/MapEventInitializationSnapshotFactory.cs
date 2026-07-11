using GameInterface.Services.MapEventParties;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using SandBox.GauntletUI.Map;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Initialization;

internal interface IMapEventInitializationSnapshotFactory : IGameAbstraction
{
    bool TryCreate(
        MapEvent mapEvent,
        bool isTerminalInitialization,
        out NetworkInitializeMapEvent message);
}

/// <summary>
/// Captures a fully registered map event into a single wire command. A missing object-manager id
/// fails the entire capture so callers never send an aggregate with dangling references.
/// </summary>
internal sealed class MapEventInitializationSnapshotFactory : IMapEventInitializationSnapshotFactory
{
    private readonly IObjectManager objectManager;

    public MapEventInitializationSnapshotFactory(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool TryCreate(
        MapEvent mapEvent,
        bool isTerminalInitialization,
        out NetworkInitializeMapEvent message)
    {
        message = null;

        if (!TryGetRequiredId(mapEvent, out var mapEventId))
            return false;

        if (!TryGetRequiredId(mapEvent.TroopUpgradeTracker, out var troopUpgradeTrackerId))
            return false;

        if (!TryGetOptionalId(mapEvent.MapEventSettlement, out var mapEventSettlementId))
            return false;

        if (!TryCreateComponent(mapEvent.Component, mapEventId, out var component))
            return false;

        if (!TryCreateSide(mapEvent.DefenderSide, mapEventId, out var defenderSide))
            return false;

        if (!TryCreateSide(mapEvent.AttackerSide, mapEventId, out var attackerSide))
            return false;

        if (mapEvent.StrengthOfSide == null || mapEvent.StrengthOfSide.Length != 2)
            return false;

        if (mapEvent.WonRounds == null)
            return false;

        var strengthOfSide = (float[])mapEvent.StrengthOfSide.Clone();
        var wonRounds = new BattleSideEnum[mapEvent.WonRounds.Count];
        for (int i = 0; i < wonRounds.Length; i++)
            wonRounds[i] = mapEvent.WonRounds[i];

        string gauntletMapEventVisualId = null;
        if (mapEvent.MapEventVisual is GauntletMapEventVisual gauntletMapEventVisual)
        {
            // Headless and early-created visuals are intentionally optional. The graph remains
            // complete without one; the client hydrator can create its local visual after wiring.
            TryGetRequiredId(gauntletMapEventVisual, out gauntletMapEventVisualId);
        }

        message = new NetworkInitializeMapEvent(
            mapEventId,
            mapEvent._nextSimulationTime,
            mapEvent._mapEventStartTime,
            mapEvent._mapEventType,
            mapEvent._eventTerrainType,
            mapEvent._state,
            mapEvent._battleState,
            mapEvent.RetreatingSide,
            mapEvent.PursuitRoundNumber,
            mapEvent.Position,
            mapEventSettlementId,
            mapEvent.DiplomaticallyFinished,
            mapEvent.IsInvulnerable,
            mapEvent.IsPlayerSimulation,
            mapEvent._mapEventResultsApplied,
            mapEvent._mapEventResultsCalculated,
            mapEvent.FirstUpdateIsDone,
            mapEvent._wasEverInLootingPhase,
            mapEvent._keepSiegeEvent,
            mapEvent._isVisible,
            mapEvent._isFinishCalled,
            mapEvent._playerFigureheadCalculated,
            strengthOfSide,
            wonRounds,
            component,
            troopUpgradeTrackerId,
            gauntletMapEventVisualId,
            defenderSide,
            attackerSide,
            isTerminalInitialization);

        return true;
    }

    private bool TryCreateComponent(
        MapEventComponent mapEventComponent,
        string mapEventId,
        out MapEventComponentInitializationData component)
    {
        component = null;
        if (mapEventComponent == null)
            return true;

        if (!TryGetRequiredId(mapEventComponent, out var componentId))
            return false;

        if (!TryGetRequiredId(mapEventComponent.MapEvent, out var componentMapEventId) ||
            componentMapEventId != mapEventId)
        {
            return false;
        }

        if (!MapEventComponentKindMapper.TryGetKind(mapEventComponent, out var kind) ||
            kind == MapEventComponentKind.None)
        {
            return false;
        }

        bool hideoutIsSendTroops = false;
        float raidNextSettlementDamage = 0f;
        int raidLootedItemCount = 0;
        RaidProductionRewardInitializationData[] raidProductionRewards = Array.Empty<RaidProductionRewardInitializationData>();
        bool raidIsMilitiaResistanceFight = false;
        float raidDamage = 0f;
        bool blockadeIsInitializationNotFinished = false;

        if (mapEventComponent is HideoutEventComponent hideoutEventComponent)
        {
            hideoutIsSendTroops = hideoutEventComponent.IsSendTroops;
        }
        else if (mapEventComponent is RaidEventComponent raidEventComponent)
        {
            raidNextSettlementDamage = raidEventComponent._nextSettlementDamage;
            raidLootedItemCount = raidEventComponent._lootedItemCount;
            raidIsMilitiaResistanceFight = raidEventComponent._isMilitiaResistanceFight;
            raidDamage = raidEventComponent.RaidDamage;

            if (!TryCreateRaidProductionRewards(
                    raidEventComponent._raidProductionRewards,
                    out raidProductionRewards))
            {
                return false;
            }
        }
        else if (mapEventComponent is BlockadeBattleMapEvent blockadeBattleMapEvent)
        {
            blockadeIsInitializationNotFinished = blockadeBattleMapEvent._isInitializationNotFinished;
        }

        component = new MapEventComponentInitializationData(
            componentId,
            componentMapEventId,
            kind,
            mapEventComponent._isFinished,
            hideoutIsSendTroops,
            raidNextSettlementDamage,
            raidLootedItemCount,
            raidProductionRewards,
            raidIsMilitiaResistanceFight,
            raidDamage,
            blockadeIsInitializationNotFinished);

        return true;
    }

    private bool TryCreateRaidProductionRewards(
        Dictionary<ItemObject, float> productionRewards,
        out RaidProductionRewardInitializationData[] rewards)
    {
        if (productionRewards == null || productionRewards.Count == 0)
        {
            rewards = Array.Empty<RaidProductionRewardInitializationData>();
            return true;
        }

        rewards = new RaidProductionRewardInitializationData[productionRewards.Count];
        int index = 0;
        foreach (var productionReward in productionRewards)
        {
            if (!TryGetRequiredId(productionReward.Key, out var itemObjectId))
            {
                rewards = null;
                return false;
            }

            rewards[index++] = new RaidProductionRewardInitializationData(
                itemObjectId,
                productionReward.Value);
        }

        return true;
    }

    private bool TryCreateSide(
        MapEventSide mapEventSide,
        string mapEventId,
        out MapEventSideInitializationData side)
    {
        side = null;
        if (!TryGetRequiredId(mapEventSide, out var mapEventSideId))
            return false;

        if (!TryGetRequiredId(mapEventSide.MapEvent, out var sideMapEventId) ||
            sideMapEventId != mapEventId)
        {
            return false;
        }

        if (!TryGetRequiredId(mapEventSide.LeaderParty, out var leaderPartyId))
            return false;

        if (!TryGetRequiredId(mapEventSide._mapFaction, out var mapFactionId))
            return false;

        if (mapEventSide.Parties == null)
            return false;

        var parties = new MapEventPartyInitializationData[mapEventSide.Parties.Count];
        for (int i = 0; i < parties.Length; i++)
        {
            if (!TryCreateParty(
                    mapEventSide.Parties[i],
                    mapEventSideId,
                    out parties[i]))
            {
                return false;
            }
        }

        side = new MapEventSideInitializationData(
            mapEventSideId,
            sideMapEventId,
            leaderPartyId,
            mapFactionId,
            mapEventSide.MissionSide,
            mapEventSide.CasualtyStrength,
            mapEventSide.LeaderSimulationModifier,
            mapEventSide.StrengthRatio,
            mapEventSide.RenownValue,
            mapEventSide.InfluenceValue,
            mapEventSide.TroopCasualties,
            mapEventSide.ShipCasualties,
            mapEventSide.IsSurrendered,
            parties);

        return true;
    }

    private bool TryCreateParty(
        MapEventParty mapEventParty,
        string mapEventSideId,
        out MapEventPartyInitializationData party)
    {
        party = null;
        if (!TryGetRequiredId(mapEventParty, out var mapEventPartyId))
            return false;

        if (!TryGetRequiredId(mapEventParty.Party, out var partyBaseId))
            return false;

        if (!TryGetRequiredId(mapEventParty._woundedInBattle, out var woundedInBattleRosterId))
            return false;

        if (!TryGetRequiredId(mapEventParty._diedInBattle, out var diedInBattleRosterId))
            return false;

        if (!TryGetRequiredId(mapEventParty._routedInBattle, out var routedInBattleRosterId))
            return false;

        if (!TrySerializeFlattenedTroops(mapEventParty._roster, out var flattenedTroops))
            return false;

        var mobileParty = mapEventParty.Party.MobileParty;
        bool hasMobileParty = mobileParty != null;
        var mobilePartyPosition = hasMobileParty ? mobileParty.Position : default;
        var eventPositionAdder = hasMobileParty ? mobileParty.EventPositionAdder : default;

        party = new MapEventPartyInitializationData(
            mapEventPartyId,
            mapEventSideId,
            partyBaseId,
            woundedInBattleRosterId,
            diedInBattleRosterId,
            routedInBattleRosterId,
            flattenedTroops,
            mapEventParty._contributionToBattle,
            mapEventParty._healthyManCountAtStart,
            mapEventParty._participatingTroopCount,
            mapEventParty.PlunderedGold,
            mapEventParty.GoldLost,
            mapEventParty.GainedRenownExplained,
            mapEventParty.GainedInfluenceExplained,
            mapEventParty.GainedMoraleExplained,
            hasMobileParty,
            mobilePartyPosition,
            eventPositionAdder.X,
            eventPositionAdder.Y);

        return true;
    }

    private bool TrySerializeFlattenedTroops(
        FlattenedTroopRoster roster,
        out FlattenedTroop[] flattenedTroops)
    {
        flattenedTroops = null;
        if (roster == null)
            return false;

        var expectedObjectIds = new List<string>();
        foreach (var element in roster)
        {
            var troop = element.Troop;
            if (troop == null)
                return false;

            var objectToResolve = troop.IsHero
                ? (object)troop.HeroObject
                : troop;

            if (!TryGetRequiredId(objectToResolve, out var objectId))
                return false;

            expectedObjectIds.Add(objectId);
        }

        var serializedTroops = FlattenedTroopSerializer.Serialize(roster, objectManager);
        if (serializedTroops.Length != expectedObjectIds.Count)
            return false;

        for (int i = 0; i < serializedTroops.Length; i++)
        {
            if (serializedTroops[i].ObjectId != expectedObjectIds[i])
                return false;
        }

        flattenedTroops = serializedTroops;
        return true;
    }

    private bool TryGetOptionalId(object instance, out string id)
    {
        if (instance == null)
        {
            id = null;
            return true;
        }

        return TryGetRequiredId(instance, out id);
    }

    private bool TryGetRequiredId(object instance, out string id)
    {
        id = null;
        return instance != null &&
               objectManager.TryGetId(instance, out id) &&
               !string.IsNullOrEmpty(id);
    }
}
