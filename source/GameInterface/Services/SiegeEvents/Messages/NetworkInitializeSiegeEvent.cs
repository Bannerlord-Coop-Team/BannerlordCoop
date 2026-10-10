using Common.Messaging;
using GameInterface.Services.SiegeEvents;
using ProtoBuf;

namespace GameInterface.Services.SiegeEvents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkInitializeSiegeEvent : IServerToClientCommand
{
    [ProtoMember(1)]
    public string SiegeEventId { get; }

    [ProtoMember(2)]
    public uint SiegeEventHandle { get; }

    [ProtoMember(3)]
    public uint SettlementHandle { get; }

    [ProtoMember(4)]
    public string BesiegerCampId { get; }

    [ProtoMember(5)]
    public uint BesiegerCampHandle { get; }

    [ProtoMember(6)]
    public uint LeaderPartyHandle { get; }

    [ProtoMember(7)]
    public string AttackerSiegeEnginesId { get; }

    [ProtoMember(8)]
    public uint AttackerSiegeEnginesHandle { get; }

    [ProtoMember(9)]
    public string DefenderSiegeEnginesId { get; }

    [ProtoMember(10)]
    public uint DefenderSiegeEnginesHandle { get; }

    [ProtoMember(11)]
    public long SiegeStartTimeTicks { get; }

    [ProtoMember(12)]
    public string BesiegerStrategyId { get; }

    [ProtoMember(13)]
    public int BesiegerTroopsKilled { get; }

    [ProtoMember(14)]
    public uint[] BesiegerPartyHandles { get; }

    [ProtoMember(15)]
    public SiegeEngineGraphSnapshot[] AttackerEngines { get; }

    [ProtoMember(16)]
    public SiegeEngineGraphSnapshot[] DefenderEngines { get; }

    public NetworkInitializeSiegeEvent(
        string siegeEventId,
        uint siegeEventHandle,
        uint settlementHandle,
        string besiegerCampId,
        uint besiegerCampHandle,
        uint leaderPartyHandle,
        string attackerSiegeEnginesId,
        uint attackerSiegeEnginesHandle,
        string defenderSiegeEnginesId,
        uint defenderSiegeEnginesHandle)
    {
        SiegeEventId = siegeEventId;
        SiegeEventHandle = siegeEventHandle;
        SettlementHandle = settlementHandle;
        BesiegerCampId = besiegerCampId;
        BesiegerCampHandle = besiegerCampHandle;
        LeaderPartyHandle = leaderPartyHandle;
        AttackerSiegeEnginesId = attackerSiegeEnginesId;
        AttackerSiegeEnginesHandle = attackerSiegeEnginesHandle;
        DefenderSiegeEnginesId = defenderSiegeEnginesId;
        DefenderSiegeEnginesHandle = defenderSiegeEnginesHandle;
        SiegeStartTimeTicks = 0;
        BesiegerStrategyId = null;
        BesiegerTroopsKilled = 0;
        BesiegerPartyHandles = null;
        AttackerEngines = null;
        DefenderEngines = null;
    }

    public NetworkInitializeSiegeEvent(SiegeEventGraphSnapshot snapshot)
        : this(
            snapshot.SiegeEventId,
            snapshot.SiegeEventHandle,
            snapshot.SettlementHandle,
            snapshot.BesiegerCampId,
            snapshot.BesiegerCampHandle,
            snapshot.LeaderPartyHandle,
            snapshot.AttackerSiegeEnginesId,
            snapshot.AttackerSiegeEnginesHandle,
            snapshot.DefenderSiegeEnginesId,
            snapshot.DefenderSiegeEnginesHandle)
    {
        SiegeStartTimeTicks = snapshot.SiegeStartTimeTicks;
        BesiegerStrategyId = snapshot.BesiegerStrategyId;
        BesiegerTroopsKilled = snapshot.BesiegerTroopsKilled;
        BesiegerPartyHandles = snapshot.BesiegerPartyHandles;
        AttackerEngines = snapshot.AttackerEngines;
        DefenderEngines = snapshot.DefenderEngines;
    }

    public SiegeEventGraphSnapshot ToSnapshot() => new SiegeEventGraphSnapshot(
        SiegeEventId,
        SiegeEventHandle,
        SettlementHandle,
        BesiegerCampId,
        BesiegerCampHandle,
        LeaderPartyHandle,
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
