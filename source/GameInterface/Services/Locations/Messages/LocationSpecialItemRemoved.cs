using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Event for when an item is removed from a <see cref="Location"/>'s special items.
/// </summary>
public readonly struct LocationSpecialItemRemoved : IEvent
{
    public readonly Location Location;
    public readonly ItemObject Item;

    public LocationSpecialItemRemoved(Location location, ItemObject item)
    {
        Location = location;
        Item = item;
    }
}
