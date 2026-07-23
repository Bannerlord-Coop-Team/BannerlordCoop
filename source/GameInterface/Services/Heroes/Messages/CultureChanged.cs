using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for Culture.
/// </summary>
public readonly struct CultureChanged : IEvent
{
    public readonly CultureObject Culture;
    public readonly Hero Hero;

    public CultureChanged(CultureObject culture, Hero hero)
    {
        Culture = culture;
        Hero = hero;
    }
}