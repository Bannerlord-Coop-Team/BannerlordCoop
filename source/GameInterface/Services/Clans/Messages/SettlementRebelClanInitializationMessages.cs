using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

internal readonly struct SettlementRebelClanInitialized : IEvent
{
    public Clan Clan { get; }

    public SettlementRebelClanInitialized(Clan clan)
    {
        Clan = clan;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkInitializeSettlementRebelClan : ICommand
{
    [ProtoMember(1)]
    public string ClanId { get; }

    [ProtoMember(2)]
    public string CultureId { get; }

    [ProtoMember(3)]
    public string LeaderId { get; }

    [ProtoMember(4)]
    public string InitialHomeSettlementId { get; }

    [ProtoMember(5)]
    public string HomeSettlementId { get; }

    [ProtoMember(6)]
    public string BannerCode { get; }

    [ProtoMember(7)]
    public int Tier { get; }

    [ProtoMember(8)]
    public uint Color { get; }

    [ProtoMember(9)]
    public uint Color2 { get; }

    [ProtoMember(10)]
    public uint BannerBackgroundColorPrimary { get; }

    [ProtoMember(11)]
    public uint BannerBackgroundColorSecondary { get; }

    [ProtoMember(12)]
    public uint BannerIconColor { get; }

    [ProtoMember(13)]
    public bool IsRebelClan { get; }

    [ProtoMember(14)]
    public bool IsNoble { get; }

    public NetworkInitializeSettlementRebelClan(
        string clanId,
        string cultureId,
        string leaderId,
        string initialHomeSettlementId,
        string homeSettlementId,
        string bannerCode,
        int tier,
        uint color,
        uint color2,
        uint bannerBackgroundColorPrimary,
        uint bannerBackgroundColorSecondary,
        uint bannerIconColor,
        bool isRebelClan,
        bool isNoble)
    {
        ClanId = clanId;
        CultureId = cultureId;
        LeaderId = leaderId;
        InitialHomeSettlementId = initialHomeSettlementId;
        HomeSettlementId = homeSettlementId;
        BannerCode = bannerCode;
        Tier = tier;
        Color = color;
        Color2 = color2;
        BannerBackgroundColorPrimary = bannerBackgroundColorPrimary;
        BannerBackgroundColorSecondary = bannerBackgroundColorSecondary;
        BannerIconColor = bannerIconColor;
        IsRebelClan = isRebelClan;
        IsNoble = isNoble;
    }
}
