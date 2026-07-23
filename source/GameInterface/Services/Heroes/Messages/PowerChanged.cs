using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for _power.
/// </summary>
public readonly struct PowerChanged : IEvent
{
    public readonly float Power;
    public readonly Hero Hero;

    public PowerChanged(float power, Hero hero)
    {
        Power = power;
        Hero = hero;
    }
}