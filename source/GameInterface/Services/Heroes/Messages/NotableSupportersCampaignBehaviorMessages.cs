using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

public readonly struct NotableSupportAccepted : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero Notable;
    public readonly Clan PlayerClan;
    public readonly int Cost;

    public NotableSupportAccepted(Hero mainHero, Hero notable, Clan playerClan, int cost)
    {
        MainHero = mainHero;
        Notable = notable;
        PlayerClan = playerClan;
        Cost = cost;
    }
}

public readonly struct NotableSupportEndedByAgreement : IEvent
{
    public readonly Hero Notable;

    public NotableSupportEndedByAgreement(Hero notable)
    {
        Notable = notable;
    }
}