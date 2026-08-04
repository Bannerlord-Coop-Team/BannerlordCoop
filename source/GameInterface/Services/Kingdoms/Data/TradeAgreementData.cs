using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Data;

[ProtoContract]
public readonly struct TradeAgreementData
{
    [ProtoMember(1)]
    public readonly string Kingdom1Id;

    [ProtoMember(2)]
    public readonly string Kingdom2Id;

    [ProtoMember(3)]
    public readonly long EndTimeNumTicks;

    [ProtoMember(4)]
    public readonly int Kingdom1GoldGained;

    [ProtoMember(5)]
    public readonly int Kingdom2GoldGained;

    [ProtoMember(6)]
    public readonly int Kingdom1GoldGainedTotal;

    [ProtoMember(7)]
    public readonly int Kingdom2GoldGainedTotal;

    public TradeAgreementData(
        string kingdom1Id,
        string kingdom2Id,
        long endTimeNumTicks,
        int kingdom1GoldGained,
        int kingdom2GoldGained,
        int kingdom1GoldGainedTotal,
        int kingdom2GoldGainedTotal)
    {
        Kingdom1Id = kingdom1Id;
        Kingdom2Id = kingdom2Id;
        EndTimeNumTicks = endTimeNumTicks;
        Kingdom1GoldGained = kingdom1GoldGained;
        Kingdom2GoldGained = kingdom2GoldGained;
        Kingdom1GoldGainedTotal = kingdom1GoldGainedTotal;
        Kingdom2GoldGainedTotal = kingdom2GoldGainedTotal;
    }
}
