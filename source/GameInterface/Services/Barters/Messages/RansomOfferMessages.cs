using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Barters.Messages;

public readonly struct RansomOfferAccepted : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero RansomHero;

    public RansomOfferAccepted(Hero mainHero, Hero ransomHero)
    {
        MainHero = mainHero;
        RansomHero = ransomHero;
    }
}

public readonly struct RansomOfferDeclined : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero RansomHero;

    public RansomOfferDeclined(Hero mainHero, Hero ransomHero)
    {
        MainHero = mainHero;
        RansomHero = ransomHero;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRansomOfferAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string RansomHeroId;

    public NetworkRansomOfferAccepted(string mainHeroId, string ransomHeroId)
    {
        MainHeroId = mainHeroId;
        RansomHeroId = ransomHeroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRansomOfferDeclined : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string RansomHeroId;

    public NetworkRansomOfferDeclined(string mainHeroId, string ransomHeroId)
    {
        MainHeroId = mainHeroId;
        RansomHeroId = ransomHeroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRansomOfferSent : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string RansomHeroId;

    [ProtoMember(3)]
    public readonly float RansomValue;

    public NetworkRansomOfferSent(
        string playerHeroId,
        string currentRansomHeroId,
        float ransomValue)
    {
        PlayerHeroId = playerHeroId;
        RansomHeroId = currentRansomHeroId;
        RansomValue = ransomValue;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRansomOfferCancelled : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string CurrentRansomHeroId;

    public NetworkRansomOfferCancelled(
        string playerHeroId,
        string currentRansomHeroId)
    {
        PlayerHeroId = playerHeroId;
        CurrentRansomHeroId = currentRansomHeroId;
    }
}