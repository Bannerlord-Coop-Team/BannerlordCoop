using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Event for when a character is removed from a <see cref="Location"/>'s character list.
/// </summary>
public readonly struct LocationCharacterRemoved : IEvent
{
    public readonly Location Location;
    public readonly CharacterObject Character;

    public LocationCharacterRemoved(Location location, CharacterObject character)
    {
        Location = location;
        Character = character;
    }
}
