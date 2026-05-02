using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for _firstName.
/// </summary>
public readonly struct FirstNameChanged : IEvent
{
    public readonly string NewName;
    public readonly Hero Hero;

    public FirstNameChanged(string newName, Hero hero)
    {
        NewName = newName;
        Hero = hero;
    }
}