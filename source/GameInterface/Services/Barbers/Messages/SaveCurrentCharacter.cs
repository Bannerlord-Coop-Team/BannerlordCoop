using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Barbers.Messages;

public readonly struct SaveCurrentCharacter : IEvent
{
    public readonly CharacterObject CharacterToChange;
    public readonly BodyProperties CurrentBodyProperties;
    public readonly int Race;
    public readonly bool IsFemale;

    public SaveCurrentCharacter(
        CharacterObject characterToChange,
        BodyProperties currentBodyProperties,
        int race,
        bool isFemale)
    {
        CharacterToChange = characterToChange;
        CurrentBodyProperties = currentBodyProperties;
        Race = race;
        IsFemale = isFemale;
    }
}
