using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Command to remove an item from a location's special items.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRemoveLocationSpecialItem : ICommand
{
    [ProtoMember(1)]
    public readonly string LocationId;
    [ProtoMember(2)]
    public readonly string ItemId;

    public NetworkRemoveLocationSpecialItem(string locationId, string itemId)
    {
        LocationId = locationId;
        ItemId = itemId;
    }
}
