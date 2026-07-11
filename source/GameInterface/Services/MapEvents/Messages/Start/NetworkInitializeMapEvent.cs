using Common.Messaging;
using GameInterface.Services.MapEventParties;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// Server -&gt; client command containing one complete, authoritative map-event object graph.
/// The client can hydrate and publish the graph atomically without depending on lifetime or
/// auto-sync packets arriving in a particular order.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal sealed class NetworkInitializeMapEvent : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly CampaignTime NextSimulationTime;
    [ProtoMember(3)]
    public readonly CampaignTime MapEventStartTime;
    [ProtoMember(4)]
    public readonly MapEvent.BattleTypes EventType;
    [ProtoMember(5)]
    public readonly TerrainType EventTerrainType;
    [ProtoMember(6)]
    public readonly MapEventState State;
    [ProtoMember(7)]
    public readonly BattleState BattleState;
    [ProtoMember(8)]
    public readonly BattleSideEnum RetreatingSide;
    [ProtoMember(9)]
    public readonly int PursuitRoundNumber;
    [ProtoMember(10)]
    public readonly CampaignVec2 Position;
    [ProtoMember(11)]
    public readonly string MapEventSettlementId;
    [ProtoMember(12)]
    public readonly bool DiplomaticallyFinished;
    [ProtoMember(13)]
    public readonly bool IsInvulnerable;
    [ProtoMember(14)]
    public readonly bool IsPlayerSimulation;
    [ProtoMember(15)]
    public readonly bool MapEventResultsApplied;
    [ProtoMember(16)]
    public readonly bool MapEventResultsCalculated;
    [ProtoMember(17)]
    public readonly bool FirstUpdateIsDone;
    [ProtoMember(18)]
    public readonly bool WasEverInLootingPhase;
    [ProtoMember(19)]
    public readonly bool KeepSiegeEvent;
    [ProtoMember(20)]
    public readonly bool IsVisible;
    [ProtoMember(21)]
    public readonly bool IsFinishCalled;
    [ProtoMember(22)]
    public readonly bool PlayerFigureheadCalculated;
    [ProtoMember(23)]
    public readonly float[] StrengthOfSide;
    [ProtoMember(24)]
    public readonly BattleSideEnum[] WonRounds;
    [ProtoMember(25)]
    public readonly MapEventComponentInitializationData Component;
    [ProtoMember(26)]
    public readonly string TroopUpgradeTrackerId;
    [ProtoMember(27)]
    public readonly string GauntletMapEventVisualId;
    [ProtoMember(28)]
    public readonly MapEventSideInitializationData DefenderSide;
    [ProtoMember(29)]
    public readonly MapEventSideInitializationData AttackerSide;
    [ProtoMember(30)]
    public readonly bool IsTerminalInitialization;

    public NetworkInitializeMapEvent(
        string mapEventId,
        CampaignTime nextSimulationTime,
        CampaignTime mapEventStartTime,
        MapEvent.BattleTypes eventType,
        TerrainType eventTerrainType,
        MapEventState state,
        BattleState battleState,
        BattleSideEnum retreatingSide,
        int pursuitRoundNumber,
        CampaignVec2 position,
        string mapEventSettlementId,
        bool diplomaticallyFinished,
        bool isInvulnerable,
        bool isPlayerSimulation,
        bool mapEventResultsApplied,
        bool mapEventResultsCalculated,
        bool firstUpdateIsDone,
        bool wasEverInLootingPhase,
        bool keepSiegeEvent,
        bool isVisible,
        bool isFinishCalled,
        bool playerFigureheadCalculated,
        float[] strengthOfSide,
        BattleSideEnum[] wonRounds,
        MapEventComponentInitializationData component,
        string troopUpgradeTrackerId,
        string gauntletMapEventVisualId,
        MapEventSideInitializationData defenderSide,
        MapEventSideInitializationData attackerSide,
        bool isTerminalInitialization)
    {
        MapEventId = mapEventId;
        NextSimulationTime = nextSimulationTime;
        MapEventStartTime = mapEventStartTime;
        EventType = eventType;
        EventTerrainType = eventTerrainType;
        State = state;
        BattleState = battleState;
        RetreatingSide = retreatingSide;
        PursuitRoundNumber = pursuitRoundNumber;
        Position = position;
        MapEventSettlementId = mapEventSettlementId;
        DiplomaticallyFinished = diplomaticallyFinished;
        IsInvulnerable = isInvulnerable;
        IsPlayerSimulation = isPlayerSimulation;
        MapEventResultsApplied = mapEventResultsApplied;
        MapEventResultsCalculated = mapEventResultsCalculated;
        FirstUpdateIsDone = firstUpdateIsDone;
        WasEverInLootingPhase = wasEverInLootingPhase;
        KeepSiegeEvent = keepSiegeEvent;
        IsVisible = isVisible;
        IsFinishCalled = isFinishCalled;
        PlayerFigureheadCalculated = playerFigureheadCalculated;
        StrengthOfSide = strengthOfSide;
        WonRounds = wonRounds;
        Component = component;
        TroopUpgradeTrackerId = troopUpgradeTrackerId;
        GauntletMapEventVisualId = gauntletMapEventVisualId;
        DefenderSide = defenderSide;
        AttackerSide = attackerSide;
        IsTerminalInitialization = isTerminalInitialization;
    }
}

[ProtoContract(SkipConstructor = true)]
internal sealed class MapEventComponentInitializationData
{
    [ProtoMember(1)]
    public readonly string ComponentId;
    [ProtoMember(2)]
    public readonly string MapEventId;
    [ProtoMember(3)]
    public readonly MapEventComponentKind Kind;
    [ProtoMember(4)]
    public readonly bool IsFinished;
    [ProtoMember(5)]
    public readonly bool HideoutIsSendTroops;
    [ProtoMember(6)]
    public readonly float RaidNextSettlementDamage;
    [ProtoMember(7)]
    public readonly int RaidLootedItemCount;
    [ProtoMember(8)]
    public readonly RaidProductionRewardInitializationData[] RaidProductionRewards;
    [ProtoMember(9)]
    public readonly bool RaidIsMilitiaResistanceFight;
    [ProtoMember(10)]
    public readonly float RaidDamage;
    [ProtoMember(11)]
    public readonly bool BlockadeIsInitializationNotFinished;

    public MapEventComponentInitializationData(
        string componentId,
        string mapEventId,
        MapEventComponentKind kind,
        bool isFinished,
        bool hideoutIsSendTroops,
        float raidNextSettlementDamage,
        int raidLootedItemCount,
        RaidProductionRewardInitializationData[] raidProductionRewards,
        bool raidIsMilitiaResistanceFight,
        float raidDamage,
        bool blockadeIsInitializationNotFinished)
    {
        ComponentId = componentId;
        MapEventId = mapEventId;
        Kind = kind;
        IsFinished = isFinished;
        HideoutIsSendTroops = hideoutIsSendTroops;
        RaidNextSettlementDamage = raidNextSettlementDamage;
        RaidLootedItemCount = raidLootedItemCount;
        RaidProductionRewards = raidProductionRewards;
        RaidIsMilitiaResistanceFight = raidIsMilitiaResistanceFight;
        RaidDamage = raidDamage;
        BlockadeIsInitializationNotFinished = blockadeIsInitializationNotFinished;
    }
}

[ProtoContract(SkipConstructor = true)]
internal sealed class RaidProductionRewardInitializationData
{
    [ProtoMember(1)]
    public readonly string ItemObjectId;
    [ProtoMember(2)]
    public readonly float Amount;

    public RaidProductionRewardInitializationData(string itemObjectId, float amount)
    {
        ItemObjectId = itemObjectId;
        Amount = amount;
    }
}

[ProtoContract(SkipConstructor = true)]
internal sealed class MapEventSideInitializationData
{
    [ProtoMember(1)]
    public readonly string MapEventSideId;
    [ProtoMember(2)]
    public readonly string MapEventId;
    [ProtoMember(3)]
    public readonly string LeaderPartyId;
    [ProtoMember(4)]
    public readonly string MapFactionId;
    [ProtoMember(5)]
    public readonly BattleSideEnum MissionSide;
    [ProtoMember(6)]
    public readonly float CasualtyStrength;
    [ProtoMember(7)]
    public readonly float LeaderSimulationModifier;
    [ProtoMember(8)]
    public readonly float StrengthRatio;
    [ProtoMember(9)]
    public readonly float RenownValue;
    [ProtoMember(10)]
    public readonly float InfluenceValue;
    [ProtoMember(11)]
    public readonly int TroopCasualties;
    [ProtoMember(12)]
    public readonly int ShipCasualties;
    [ProtoMember(13)]
    public readonly bool IsSurrendered;
    [ProtoMember(14)]
    public readonly MapEventPartyInitializationData[] Parties;

    public MapEventSideInitializationData(
        string mapEventSideId,
        string mapEventId,
        string leaderPartyId,
        string mapFactionId,
        BattleSideEnum missionSide,
        float casualtyStrength,
        float leaderSimulationModifier,
        float strengthRatio,
        float renownValue,
        float influenceValue,
        int troopCasualties,
        int shipCasualties,
        bool isSurrendered,
        MapEventPartyInitializationData[] parties)
    {
        MapEventSideId = mapEventSideId;
        MapEventId = mapEventId;
        LeaderPartyId = leaderPartyId;
        MapFactionId = mapFactionId;
        MissionSide = missionSide;
        CasualtyStrength = casualtyStrength;
        LeaderSimulationModifier = leaderSimulationModifier;
        StrengthRatio = strengthRatio;
        RenownValue = renownValue;
        InfluenceValue = influenceValue;
        TroopCasualties = troopCasualties;
        ShipCasualties = shipCasualties;
        IsSurrendered = isSurrendered;
        Parties = parties;
    }
}

[ProtoContract(SkipConstructor = true)]
internal sealed class MapEventPartyInitializationData
{
    [ProtoMember(1)]
    public readonly string MapEventPartyId;
    [ProtoMember(2)]
    public readonly string MapEventSideId;
    [ProtoMember(3)]
    public readonly string PartyBaseId;
    [ProtoMember(4)]
    public readonly string WoundedInBattleRosterId;
    [ProtoMember(5)]
    public readonly string DiedInBattleRosterId;
    [ProtoMember(6)]
    public readonly string RoutedInBattleRosterId;
    [ProtoMember(7)]
    public readonly FlattenedTroop[] FlattenedTroops;
    [ProtoMember(8)]
    public readonly int ContributionToBattle;
    [ProtoMember(9)]
    public readonly int HealthyManCountAtStart;
    [ProtoMember(10)]
    public readonly int ParticipatingTroopCount;
    [ProtoMember(11)]
    public readonly int PlunderedGold;
    [ProtoMember(12)]
    public readonly int GoldLost;
    [ProtoMember(13)]
    public readonly ExplainedNumber GainedRenownExplained;
    [ProtoMember(14)]
    public readonly ExplainedNumber GainedInfluenceExplained;
    [ProtoMember(15)]
    public readonly ExplainedNumber GainedMoraleExplained;
    [ProtoMember(16)]
    public readonly bool HasMobileParty;
    [ProtoMember(17)]
    public readonly CampaignVec2 MobilePartyPosition;
    [ProtoMember(18)]
    public readonly float EventPositionAdderX;
    [ProtoMember(19)]
    public readonly float EventPositionAdderY;

    public MapEventPartyInitializationData(
        string mapEventPartyId,
        string mapEventSideId,
        string partyBaseId,
        string woundedInBattleRosterId,
        string diedInBattleRosterId,
        string routedInBattleRosterId,
        FlattenedTroop[] flattenedTroops,
        int contributionToBattle,
        int healthyManCountAtStart,
        int participatingTroopCount,
        int plunderedGold,
        int goldLost,
        ExplainedNumber gainedRenownExplained,
        ExplainedNumber gainedInfluenceExplained,
        ExplainedNumber gainedMoraleExplained,
        bool hasMobileParty,
        CampaignVec2 mobilePartyPosition,
        float eventPositionAdderX,
        float eventPositionAdderY)
    {
        MapEventPartyId = mapEventPartyId;
        MapEventSideId = mapEventSideId;
        PartyBaseId = partyBaseId;
        WoundedInBattleRosterId = woundedInBattleRosterId;
        DiedInBattleRosterId = diedInBattleRosterId;
        RoutedInBattleRosterId = routedInBattleRosterId;
        FlattenedTroops = flattenedTroops;
        ContributionToBattle = contributionToBattle;
        HealthyManCountAtStart = healthyManCountAtStart;
        ParticipatingTroopCount = participatingTroopCount;
        PlunderedGold = plunderedGold;
        GoldLost = goldLost;
        GainedRenownExplained = gainedRenownExplained;
        GainedInfluenceExplained = gainedInfluenceExplained;
        GainedMoraleExplained = gainedMoraleExplained;
        HasMobileParty = hasMobileParty;
        MobilePartyPosition = mobilePartyPosition;
        EventPositionAdderX = eventPositionAdderX;
        EventPositionAdderY = eventPositionAdderY;
    }
}
