using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Party.Messages;

public readonly struct HeroExecuted : IEvent
{
    public readonly Hero ExecutedHero;
    public readonly Hero Executor;

    public HeroExecuted(Hero executedHero, Hero executor)
    {
        ExecutedHero = executedHero;
        Executor = executor;
    }
}