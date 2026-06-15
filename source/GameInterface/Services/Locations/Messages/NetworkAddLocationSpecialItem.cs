using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Command to add an item to a location's special items.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAddLocationSpecialItem : ICommand
{
    [ProtoMember(1)]
    public readonly string LocationId;
    [ProtoMember(2)]
    public readonly string ItemId;

    public NetworkAddLocationSpecialItem(string locationId, string itemId)
    {
        LocationId = locationId;
        ItemId = itemId;
    }
}
