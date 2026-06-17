using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Command to clear a location's character list.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRemoveAllLocationCharacters : ICommand
{
    [ProtoMember(1)]
    public readonly string LocationId;

    public NetworkRemoveAllLocationCharacters(string locationId)
    {
        LocationId = locationId;
    }
}
