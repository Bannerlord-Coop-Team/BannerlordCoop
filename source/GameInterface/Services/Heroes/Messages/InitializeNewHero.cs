using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

public readonly struct InitializeNewHero : IEvent
{
    public readonly Hero Hero;

    public InitializeNewHero(Hero hero)
    {
        Hero = hero;
    }
}
