using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Players.Messages;

public readonly struct UpdateHasMetHermit : IEvent
{
    public readonly Hero MainHero;
    public readonly bool HasMetHermit;

    public UpdateHasMetHermit(Hero mainHero, bool hasMetHermit)
    {
        MainHero = mainHero;
        HasMetHermit = hasMetHermit;
    }
}
