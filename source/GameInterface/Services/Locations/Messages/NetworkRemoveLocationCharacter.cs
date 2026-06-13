using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Command to remove a character from a location's character list.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRemoveLocationCharacter : ICommand
{
    [ProtoMember(1)]
    public readonly string LocationId;
    [ProtoMember(2)]
    public readonly string CharacterId;

    public NetworkRemoveLocationCharacter(string locationId, string characterId)
    {
        LocationId = locationId;
        CharacterId = characterId;
    }
}
