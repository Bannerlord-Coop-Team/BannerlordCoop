using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateTradeAgreement : ICommand
{
    [ProtoMember(1)]
    public readonly TradeAgreementData TradeAgreementData;

    public NetworkUpdateTradeAgreement(
        TradeAgreementData tradeAgreementData)
    {
        TradeAgreementData = tradeAgreementData;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkClientAcceptsTradeAgreementOffer : ICommand
{
    [ProtoMember(1)]
    public readonly string FromKingdomId;

    [ProtoMember(2)]
    public readonly string PlayerKingdomId;

    public NetworkClientAcceptsTradeAgreementOffer(
        string fromKingdomId,
        string playerKingdomId)
    {
        FromKingdomId = fromKingdomId;
        PlayerKingdomId = playerKingdomId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTradeGoldDistributedInKingdom : ICommand
{
    [ProtoMember(1)]
    public readonly string Kingdom1Id;

    [ProtoMember(2)]
    public readonly string Kingdom2Id;

    [ProtoMember(3)]
    public readonly string ClanId;

    [ProtoMember(4)]
    public readonly int Share;

    public NetworkTradeGoldDistributedInKingdom(
        string kingdom1Id,
        string kingdom2Id,
        string clanId,
        int share)
    {
        Kingdom1Id = kingdom1Id;
        Kingdom2Id = kingdom2Id;
        ClanId = clanId;
        Share = share;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMakeTradeAgreement : ICommand
{
    [ProtoMember(1)]
    public readonly TradeAgreementData NewTradeAgreementData;

    public NetworkMakeTradeAgreement(
        TradeAgreementData newTradeAgreementData)
    {
        NewTradeAgreementData = newTradeAgreementData;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRemoveTradeAgreement : ICommand
{
    [ProtoMember(1)]
    public readonly string Kingdom1Id;

    [ProtoMember(2)]
    public readonly string Kingdom2Id;

    public NetworkRemoveTradeAgreement(
        string kingdom1Id,
        string kingdom2Id)
    {
        Kingdom1Id = kingdom1Id;
        Kingdom2Id = kingdom2Id;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkEndAllTradeAgreements : ICommand
{
    [ProtoMember(1)]
    public readonly string KingdomId;

    public NetworkEndAllTradeAgreements(
        string kingdomId)
    {
        KingdomId = kingdomId;
    }
}