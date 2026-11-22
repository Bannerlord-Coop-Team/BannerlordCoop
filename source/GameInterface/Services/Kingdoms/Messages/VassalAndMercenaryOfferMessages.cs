using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages;

public readonly struct MercenaryOfferAccepted : IEvent
{
    public readonly Hero MainHero;
    public readonly Kingdom Kingdom;

    public MercenaryOfferAccepted(Hero mainHero, Kingdom kingdom)
    {
        MainHero = mainHero;
        Kingdom = kingdom;
    }
}

public readonly struct VassalOrMercenaryOfferDeclined : IEvent
{
    public readonly Hero MainHero;
    public readonly Kingdom Kingdom;

    public VassalOrMercenaryOfferDeclined(Hero mainHero, Kingdom kingdom)
    {
        MainHero = mainHero;
        Kingdom = kingdom;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMercenaryOfferAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string KingdomId;

    public NetworkMercenaryOfferAccepted(string mainHeroId, string kingdomId)
    {
        MainHeroId = mainHeroId;
        KingdomId = kingdomId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkVassalOrMercenaryOfferDeclined : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string KingdomId;

    public NetworkVassalOrMercenaryOfferDeclined(string mainHeroId, string kingdomId)
    {
        MainHeroId = mainHeroId;
        KingdomId = kingdomId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMercenaryOfferSent : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string KingdomId;

    public NetworkMercenaryOfferSent(string playerHeroId, string kingdomId)
    {
        PlayerHeroId = playerHeroId;
        KingdomId = kingdomId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkVassalOfferSent : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string KingdomId;

    public NetworkVassalOfferSent(string playerHeroId, string kingdomId)
    {
        PlayerHeroId = playerHeroId;
        KingdomId = kingdomId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkVassalOrMercenaryOfferCancelled : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string KingdomId;

    public NetworkVassalOrMercenaryOfferCancelled(string playerHeroId, string kingdomId)
    {
        PlayerHeroId = playerHeroId;
        KingdomId = kingdomId;
    }
}
