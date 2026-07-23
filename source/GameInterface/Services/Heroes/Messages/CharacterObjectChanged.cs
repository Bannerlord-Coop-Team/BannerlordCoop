using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Event from GameInterface for _characterObject.
/// </summary>
public readonly struct CharacterObjectChanged : IEvent
{
    public readonly CharacterObject CharacterObject;
    public readonly Hero Hero;

    public CharacterObjectChanged(CharacterObject characterObject, Hero hero)
    {
        CharacterObject = characterObject;
        Hero = hero;
    }
}