using Common.Messaging;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages;

public readonly struct DailyTickDrinkThisDayInSettlement : IEvent {}

public readonly struct WeeklyTickHasBoughtTunToParty : IEvent {}

public readonly struct PlayerAcceptsClanInfoOffer : IEvent
{
    public readonly Hero MainHero;
    public readonly Settlement CurrentSettlement;

    public PlayerAcceptsClanInfoOffer(
        Hero mainHero,
        Settlement currentSettlement)
    {
        MainHero = mainHero;
        CurrentSettlement = currentSettlement;
    }
}

public readonly struct TavernMaidDeliversFood : IEvent
{
    public readonly Hero MainHero;
    public readonly Settlement CurrentSettlement;

    public TavernMaidDeliversFood(
        Hero mainHero,
        Settlement currentSettlement)
    {
        MainHero = mainHero;
        CurrentSettlement = currentSettlement;
    }
}

public readonly struct PlayerBuysTun : IEvent
{
    public readonly Hero MainHero;
    public readonly int TunPrice;

    public PlayerBuysTun(
        Hero mainHero,
        int tunPrice)
    {
        MainHero = mainHero;
        TunPrice = tunPrice;
    }
}

public readonly struct UpdateHasMetRansomBroker : IEvent
{
    public readonly Hero MainHero;
    public readonly bool HasMetRansomBroker;

    public UpdateHasMetRansomBroker(
        Hero mainHero,
        bool hasMetRansomBroker)
    {
        MainHero = mainHero;
        HasMetRansomBroker = hasMetRansomBroker;
    }
}

public readonly struct TavernKeeperFindCompanion : IEvent
{
    public readonly Hero MainHero;

    public TavernKeeperFindCompanion(Hero mainHero)
    {
        MainHero = mainHero;
    }
}