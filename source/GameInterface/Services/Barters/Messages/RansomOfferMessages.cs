using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Barters.Messages;

public readonly struct RansomOfferAccepted : IEvent
{
    public readonly Hero MainHero;

    public RansomOfferAccepted(Hero mainHero)
    {
        MainHero = mainHero;
    }
}

public readonly struct RansomOfferDeclined : IEvent
{
    public readonly Hero MainHero;

    public RansomOfferDeclined(Hero mainHero)
    {
        MainHero = mainHero;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRansomOfferAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    public NetworkRansomOfferAccepted(string mainHeroId)
    {
        MainHeroId = mainHeroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRansomOfferDeclined : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    public NetworkRansomOfferDeclined(string mainHeroId)
    {
        MainHeroId = mainHeroId;
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