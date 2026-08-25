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
    public string SettlementId { get; }

    [ProtoMember(3)]
    public string BesiegerCampId { get; }

    [ProtoMember(4)]
    public string LeaderPartyId { get; }

    [ProtoMember(5)]
    public string AttackerSiegeEnginesId { get; }

    [ProtoMember(6)]
    public string DefenderSiegeEnginesId { get; }

    [ProtoMember(7)]
    public long SiegeStartTimeTicks { get; }

    [ProtoMember(8)]
    public string BesiegerStrategyId { get; }

    [ProtoMember(9)]
    public int BesiegerTroopsKilled { get; }

    [ProtoMember(10)]
    public string[] BesiegerPartyIds { get; }

    [ProtoMember(11)]
    public SiegeEngineGraphSnapshot[] AttackerEngines { get; }

    [ProtoMember(12)]
    public SiegeEngineGraphSnapshot[] DefenderEngines { get; }

    public NetworkInitializeSiegeEvent(
        string siegeEventId,
        string settlementId,
        string besiegerCampId,
        string leaderPartyId,
        string attackerSiegeEnginesId,
        string defenderSiegeEnginesId)
    {
        SiegeEventId = siegeEventId;
        SettlementId = settlementId;
        BesiegerCampId = besiegerCampId;
        LeaderPartyId = leaderPartyId;
        AttackerSiegeEnginesId = attackerSiegeEnginesId;
        DefenderSiegeEnginesId = defenderSiegeEnginesId;
        SiegeStartTimeTicks = 0;
        BesiegerStrategyId = null;
        BesiegerTroopsKilled = 0;
        BesiegerPartyIds = null;
        AttackerEngines = null;
        DefenderEngines = null;
    }

    public NetworkInitializeSiegeEvent(SiegeEventGraphSnapshot snapshot)
        : this(
            snapshot.SiegeEventId,
            snapshot.SettlementId,
            snapshot.BesiegerCampId,
            snapshot.LeaderPartyId,
            snapshot.AttackerSiegeEnginesId,
            snapshot.DefenderSiegeEnginesId)
    {
        SiegeStartTimeTicks = snapshot.SiegeStartTimeTicks;
        BesiegerStrategyId = snapshot.BesiegerStrategyId;
        BesiegerTroopsKilled = snapshot.BesiegerTroopsKilled;
        BesiegerPartyIds = snapshot.BesiegerPartyIds;
        AttackerEngines = snapshot.AttackerEngines;
        DefenderEngines = snapshot.DefenderEngines;
    }

    public SiegeEventGraphSnapshot ToSnapshot() => new SiegeEventGraphSnapshot(
        SiegeEventId,
        SettlementId,
        BesiegerCampId,
        LeaderPartyId,
        AttackerSiegeEnginesId,
        DefenderSiegeEnginesId,
        SiegeStartTimeTicks,
        BesiegerStrategyId,
        BesiegerTroopsKilled,
        BesiegerPartyIds,
        AttackerEngines,
        DefenderEngines);
}
