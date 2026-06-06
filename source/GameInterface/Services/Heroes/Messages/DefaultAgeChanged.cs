using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for _defaultAge.
/// </summary>
public readonly struct DefaultAgeChanged : IEvent
{
    public readonly float Age;
    public readonly Hero Hero;

    public DefaultAgeChanged(float age, Hero hero)
    {
        Age = age;
        Hero = hero;
    }
}