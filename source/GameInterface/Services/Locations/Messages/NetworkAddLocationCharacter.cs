using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Command to add a character to a location's character list.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAddLocationCharacter : ICommand
{
    [ProtoMember(1)]
    public readonly LocationCharacterData Data;

    public NetworkAddLocationCharacter(LocationCharacterData data)
    {
        Data = data;
    }
}
