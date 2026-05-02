using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for _birthDay.
/// </summary>
public readonly struct BirthDayChanged : IEvent
{
    public readonly long BirthDay;
    public readonly Hero Hero;

    public BirthDayChanged(long birthDay, Hero hero)
    {
        BirthDay = birthDay;
        Hero = hero;
    }
}