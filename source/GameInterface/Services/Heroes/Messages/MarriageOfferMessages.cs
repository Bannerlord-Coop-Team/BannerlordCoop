using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

public readonly struct MarriageOfferAccepted : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero PlayerClanHero;
    public readonly Hero OtherClanHero;

    public MarriageOfferAccepted(Hero mainHero, Hero playerClanHero, Hero otherClanHero)
    {
        MainHero = mainHero;
        PlayerClanHero = playerClanHero;
        OtherClanHero = otherClanHero;
    }
}

public readonly struct MarriageOfferDeclined : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero PlayerClanHero;
    public readonly Hero OtherClanHero;

    public MarriageOfferDeclined(Hero mainHero, Hero playerClanHero, Hero otherClanHero)
    {
        MainHero = mainHero;
        PlayerClanHero = playerClanHero;
        OtherClanHero = otherClanHero;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMarriageOfferAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string PlayerClanHeroId;

    [ProtoMember(3)]
    public readonly string OtherClanHeroId;

    public NetworkMarriageOfferAccepted(string mainHeroId, string playerClanHeroId, string otherClanHeroId)
    {
        MainHeroId = mainHeroId;
        PlayerClanHeroId = playerClanHeroId;
        OtherClanHeroId = otherClanHeroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMarriageOfferDeclined : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string PlayerClanHeroId;

    [ProtoMember(3)]
    public readonly string OtherClanHeroId;

    public NetworkMarriageOfferDeclined(string mainHeroId, string playerClanHeroId, string otherClanHeroId)
    {
        MainHeroId = mainHeroId;
        PlayerClanHeroId = playerClanHeroId;
        OtherClanHeroId = otherClanHeroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMarriageOfferSent : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string PlayerClanHeroId;

    [ProtoMember(3)]
    public readonly string OtherClanHeroId;

    public NetworkMarriageOfferSent(
        string playerHeroId,
        string playerClanHeroId,
        string otherClanHeroId)
    {
        PlayerHeroId = playerHeroId;
        PlayerClanHeroId = playerClanHeroId;
        OtherClanHeroId = otherClanHeroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMarriageOfferCanceled : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string PlayerClanHeroId;

    [ProtoMember(3)]
    public readonly string OtherClanHeroId;

    public NetworkMarriageOfferCanceled(
        string playerHeroId,
        string playerClanHeroId,
        string otherClanHeroId)
    {
        PlayerHeroId = playerHeroId;
        PlayerClanHeroId = playerClanHeroId;
        OtherClanHeroId = otherClanHeroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMarriageOfferAcceptResolved : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string PlayerClanHeroId;

    [ProtoMember(3)]
    public readonly string OtherClanHeroId;

    // False when the couple isn't available yet and the marriage waits until they are
    [ProtoMember(4)]
    public readonly bool Married;

    public NetworkMarriageOfferAcceptResolved(
        string playerHeroId,
        string playerClanHeroId,
        string otherClanHeroId,
        bool married)
    {
        PlayerHeroId = playerHeroId;
        PlayerClanHeroId = playerClanHeroId;
        OtherClanHeroId = otherClanHeroId;
        Married = married;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMarriageEngagementEnded : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerClanHeroId;

    public NetworkMarriageEngagementEnded(string playerClanHeroId)
    {
        PlayerClanHeroId = playerClanHeroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMarriagePreparationCanceled : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string DeadHeroId;

    [ProtoMember(3)]
    public readonly string OtherHeroId;

    public NetworkMarriagePreparationCanceled(
        string playerHeroId,
        string deadHeroId,
        string otherHeroId)
    {
        PlayerHeroId = playerHeroId;
        DeadHeroId = deadHeroId;
        OtherHeroId = otherHeroId;
    }
}
