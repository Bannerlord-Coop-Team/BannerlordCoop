using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for SpcDaysInLocation.
/// </summary>
public readonly struct SpcDaysInLocationChanged : IEvent
{
    public readonly int Days;
    public readonly Hero Hero;

    public SpcDaysInLocationChanged(int days, Hero hero)
    {
        Days = days;
        Hero = hero;
    }
}