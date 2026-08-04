using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Messages;

public readonly struct UpdateTradeAgreement : IEvent
{
    public readonly TradeAgreementsCampaignBehavior.TradeAgreement TradeAgreement;
    public readonly Settlement Settlement;
    public readonly MobileParty MobileParty;

    public UpdateTradeAgreement(
        TradeAgreementsCampaignBehavior.TradeAgreement tradeAgreement,
        Settlement settlement,
        MobileParty mobileParty)
    {
        TradeAgreement = tradeAgreement;
        Settlement = settlement;
        MobileParty = mobileParty;
    }
}

public readonly struct ClientAcceptsTradeAgreementOffer : IEvent
{
    public readonly Kingdom FromKingdom;
    public readonly Kingdom PlayerKingdom;

    public ClientAcceptsTradeAgreementOffer(Kingdom fromKingdom, Kingdom playerKingdom)
    {
        FromKingdom = fromKingdom;
        PlayerKingdom = playerKingdom;
    }
}

public readonly struct TradeGoldDistributedInKingdom : IEvent
{
    public readonly Kingdom Kingdom1;
    public readonly Kingdom Kingdom2;
    public readonly Clan Clan;
    public readonly int Share;

    public TradeGoldDistributedInKingdom(
        Kingdom kingdom1,
        Kingdom kingdom2,
        Clan clan,
        int share)
    {
        Kingdom1 = kingdom1;
        Kingdom2 = kingdom2;
        Clan = clan;
        Share = share;
    }
}

public readonly struct MakeTradeAgreement : IEvent
{
    public readonly TradeAgreementsCampaignBehavior.TradeAgreement NewTradeAgreement;

    public MakeTradeAgreement(
        TradeAgreementsCampaignBehavior.TradeAgreement newTradeAgreement)
    {
        NewTradeAgreement = newTradeAgreement;
    }
}

public readonly struct RemoveTradeAgreement : IEvent
{
    public readonly Kingdom Kingdom1;
    public readonly Kingdom Kingdom2;

    public RemoveTradeAgreement(
        Kingdom kingdom1,
        Kingdom kingdom2)
    {
        Kingdom1 = kingdom1;
        Kingdom2 = kingdom2;
    }
}

public readonly struct EndAllTradeAgreements : IEvent
{
    public readonly Kingdom Kingdom;

    public EndAllTradeAgreements(
        Kingdom kingdom) : this()
    {
        Kingdom = kingdom;
    }
}