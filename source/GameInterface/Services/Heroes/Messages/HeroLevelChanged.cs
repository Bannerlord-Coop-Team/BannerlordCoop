using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for Level.
/// </summary>
public readonly struct HeroLevelChanged : IEvent
{
    public readonly int HeroLevel;
    public readonly Hero Hero;

    public HeroLevelChanged(int heroLevel, Hero hero)
    {
        HeroLevel = heroLevel;
        Hero = hero;
    }
}