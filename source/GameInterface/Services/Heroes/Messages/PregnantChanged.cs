using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for IsPregnant.
/// </summary>
public readonly struct PregnantChanged : IEvent
{
    public readonly Hero Hero;
    public readonly bool IsPregnant;

    public PregnantChanged(Hero hero, bool isPregnant)
    {
        Hero = hero;
        IsPregnant = isPregnant;
    }
}