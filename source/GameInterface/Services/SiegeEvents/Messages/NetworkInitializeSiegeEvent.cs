using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.SiegeEvents.Messages;

[ProtoContract(SkipConstructor = true)]
internal record NetworkInitializeSiegeEvent : IEvent
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
    }
}
