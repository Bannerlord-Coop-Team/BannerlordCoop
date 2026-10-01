using Common.Messaging;
using GameInterface.Services.SiegeEvents;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkMapEventInitialized : ICommand
{
    [ProtoMember(1)]
    public readonly uint MapEventHandle;

    [ProtoMember(2)]
    public readonly bool IsTerminal;

    [ProtoMember(3)]
    public readonly uint TroopUpgradeTrackerHandle;

    [ProtoMember(4)]
    public readonly uint ComponentHandle;

    [ProtoMember(5)]
    public readonly uint VisualHandle;

    [ProtoMember(6)]
    public readonly string SiegeEventId;

    [ProtoMember(7)]
    public readonly uint SiegeEventHandle;

    [ProtoMember(8)]
    public readonly uint SiegeSettlementHandle;

    [ProtoMember(9)]
    public readonly string BesiegerCampId;

    [ProtoMember(10)]
    public readonly uint BesiegerCampHandle;

    [ProtoMember(11)]
    public readonly uint SiegeLeaderPartyHandle;

    [ProtoMember(12)]
    public readonly string AttackerSiegeEnginesId;

    [ProtoMember(13)]
    public readonly uint AttackerSiegeEnginesHandle;

    [ProtoMember(14)]
    public readonly string DefenderSiegeEnginesId;

    [ProtoMember(15)]
    public readonly uint DefenderSiegeEnginesHandle;

    [ProtoMember(16)]
    public readonly long SiegeStartTimeTicks;

    [ProtoMember(17)]
    public readonly string BesiegerStrategyId;

    [ProtoMember(18)]
    public readonly int BesiegerTroopsKilled;

    [ProtoMember(19)]
    public readonly uint[] BesiegerPartyHandles;

    [ProtoMember(20)]
    public readonly SiegeEngineGraphSnapshot[] AttackerEngines;

    [ProtoMember(21)]
    public readonly SiegeEngineGraphSnapshot[] DefenderEngines;

    public NetworkMapEventInitialized(
        uint mapEventHandle,
        bool isTerminal,
        uint troopUpgradeTrackerHandle = 0,
        uint componentHandle = 0,
        uint visualHandle = 0)
    {
        MapEventHandle = mapEventHandle;
        IsTerminal = isTerminal;
        TroopUpgradeTrackerHandle = troopUpgradeTrackerHandle;
        ComponentHandle = componentHandle;
        VisualHandle = visualHandle;
        SiegeEventId = null;
        SiegeEventHandle = 0;
        SiegeSettlementHandle = 0;
        BesiegerCampId = null;
        BesiegerCampHandle = 0;
        SiegeLeaderPartyHandle = 0;
        AttackerSiegeEnginesId = null;
        AttackerSiegeEnginesHandle = 0;
        DefenderSiegeEnginesId = null;
        DefenderSiegeEnginesHandle = 0;
        SiegeStartTimeTicks = 0;
        BesiegerStrategyId = null;
        BesiegerTroopsKilled = 0;
        BesiegerPartyHandles = null;
        AttackerEngines = null;
        DefenderEngines = null;
    }

    internal NetworkMapEventInitialized(
        uint mapEventHandle,
        bool isTerminal,
        uint troopUpgradeTrackerHandle,
        uint componentHandle,
        uint visualHandle,
        SiegeEventGraphSnapshot siegeGraph)
        : this(mapEventHandle, isTerminal, troopUpgradeTrackerHandle, componentHandle, visualHandle)
    {
        SiegeEventId = siegeGraph.SiegeEventId;
        SiegeEventHandle = siegeGraph.SiegeEventHandle;
        SiegeSettlementHandle = siegeGraph.SettlementHandle;
        BesiegerCampId = siegeGraph.BesiegerCampId;
        BesiegerCampHandle = siegeGraph.BesiegerCampHandle;
        SiegeLeaderPartyHandle = siegeGraph.LeaderPartyHandle;
        AttackerSiegeEnginesId = siegeGraph.AttackerSiegeEnginesId;
        AttackerSiegeEnginesHandle = siegeGraph.AttackerSiegeEnginesHandle;
        DefenderSiegeEnginesId = siegeGraph.DefenderSiegeEnginesId;
        DefenderSiegeEnginesHandle = siegeGraph.DefenderSiegeEnginesHandle;
        SiegeStartTimeTicks = siegeGraph.SiegeStartTimeTicks;
        BesiegerStrategyId = siegeGraph.BesiegerStrategyId;
        BesiegerTroopsKilled = siegeGraph.BesiegerTroopsKilled;
        BesiegerPartyHandles = siegeGraph.BesiegerPartyHandles;
        AttackerEngines = siegeGraph.AttackerEngines;
        DefenderEngines = siegeGraph.DefenderEngines;
    }

    internal SiegeEventGraphSnapshot SiegeGraph => new SiegeEventGraphSnapshot(
        SiegeEventId,
        SiegeEventHandle,
        SiegeSettlementHandle,
        BesiegerCampId,
        BesiegerCampHandle,
        SiegeLeaderPartyHandle,
        AttackerSiegeEnginesId,
        AttackerSiegeEnginesHandle,
        DefenderSiegeEnginesId,
        DefenderSiegeEnginesHandle,
        SiegeStartTimeTicks,
        BesiegerStrategyId,
        BesiegerTroopsKilled,
        BesiegerPartyHandles,
        AttackerEngines,
        DefenderEngines);
}
