using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for _heroState.
/// </summary>
public readonly struct HeroStateChanged : IEvent
{
    public readonly int HeroState;
    public readonly Hero Hero;

    public HeroStateChanged(int heroState, Hero hero)
    {
        HeroState = heroState;
        Hero = hero;
    }
}