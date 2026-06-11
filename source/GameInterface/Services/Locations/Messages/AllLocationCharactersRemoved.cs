using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements.Locations;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Event for when a <see cref="Location"/>'s character list is cleared.
/// </summary>
public readonly struct AllLocationCharactersRemoved : IEvent
{
    public readonly Location Location;

    public AllLocationCharactersRemoved(Location location)
    {
        Location = location;
    }
}
