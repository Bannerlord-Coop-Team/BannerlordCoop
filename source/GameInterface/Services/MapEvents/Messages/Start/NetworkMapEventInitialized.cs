using Common.Messaging;
using GameInterface.Services.SiegeEvents;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkMapEventInitialized : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    [ProtoMember(2)]
    public readonly bool IsTerminal;

    [ProtoMember(3)]
    public readonly string TroopUpgradeTrackerId;

    [ProtoMember(4)]
    public readonly string ComponentId;

    [ProtoMember(5)]
    public readonly string VisualId;

    [ProtoMember(6)]
    public readonly string SiegeEventId;

    [ProtoMember(7)]
    public readonly string SiegeSettlementId;

    [ProtoMember(8)]
    public readonly string BesiegerCampId;

    [ProtoMember(9)]
    public readonly string SiegeLeaderPartyId;

    [ProtoMember(10)]
    public readonly string AttackerSiegeEnginesId;

    [ProtoMember(11)]
    public readonly string DefenderSiegeEnginesId;

    [ProtoMember(12)]
    public readonly long SiegeStartTimeTicks;

    [ProtoMember(13)]
    public readonly string BesiegerStrategyId;

    [ProtoMember(14)]
    public readonly int BesiegerTroopsKilled;

    [ProtoMember(15)]
    public readonly string[] BesiegerPartyIds;

    [ProtoMember(16)]
    public readonly SiegeEngineGraphSnapshot[] AttackerEngines;

    [ProtoMember(17)]
    public readonly SiegeEngineGraphSnapshot[] DefenderEngines;

    public NetworkMapEventInitialized(
        string mapEventId,
        bool isTerminal,
        string troopUpgradeTrackerId = null,
        string componentId = null,
        string visualId = null)
    {
        MapEventId = mapEventId;
        IsTerminal = isTerminal;
        TroopUpgradeTrackerId = troopUpgradeTrackerId;
        ComponentId = componentId;
        VisualId = visualId;
        SiegeEventId = null;
        SiegeSettlementId = null;
        BesiegerCampId = null;
        SiegeLeaderPartyId = null;
        AttackerSiegeEnginesId = null;
        DefenderSiegeEnginesId = null;
        SiegeStartTimeTicks = 0;
        BesiegerStrategyId = null;
        BesiegerTroopsKilled = 0;
        BesiegerPartyIds = null;
        AttackerEngines = null;
        DefenderEngines = null;
    }

    internal NetworkMapEventInitialized(
        string mapEventId,
        bool isTerminal,
        string troopUpgradeTrackerId,
        string componentId,
        string visualId,
        SiegeEventGraphSnapshot siegeGraph)
        : this(mapEventId, isTerminal, troopUpgradeTrackerId, componentId, visualId)
    {
        SiegeEventId = siegeGraph.SiegeEventId;
        SiegeSettlementId = siegeGraph.SettlementId;
        BesiegerCampId = siegeGraph.BesiegerCampId;
        SiegeLeaderPartyId = siegeGraph.LeaderPartyId;
        AttackerSiegeEnginesId = siegeGraph.AttackerSiegeEnginesId;
        DefenderSiegeEnginesId = siegeGraph.DefenderSiegeEnginesId;
        SiegeStartTimeTicks = siegeGraph.SiegeStartTimeTicks;
        BesiegerStrategyId = siegeGraph.BesiegerStrategyId;
        BesiegerTroopsKilled = siegeGraph.BesiegerTroopsKilled;
        BesiegerPartyIds = siegeGraph.BesiegerPartyIds;
        AttackerEngines = siegeGraph.AttackerEngines;
        DefenderEngines = siegeGraph.DefenderEngines;
    }

    internal SiegeEventGraphSnapshot SiegeGraph => new SiegeEventGraphSnapshot(
        SiegeEventId,
        SiegeSettlementId,
        BesiegerCampId,
        SiegeLeaderPartyId,
        AttackerSiegeEnginesId,
        DefenderSiegeEnginesId,
        SiegeStartTimeTicks,
        BesiegerStrategyId,
        BesiegerTroopsKilled,
        BesiegerPartyIds,
        AttackerEngines,
        DefenderEngines);
}
