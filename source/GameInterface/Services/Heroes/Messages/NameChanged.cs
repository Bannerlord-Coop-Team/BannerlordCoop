using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for _name.
/// </summary>
public readonly struct NameChanged : IEvent
{
    public readonly string NewName;
    public readonly Hero Hero;

    public NameChanged(string newName, Hero hero)
    {
        NewName = newName;
        Hero = hero;
    }
}