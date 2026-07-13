using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Barbers.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRefreshCharacter : ICommand
{
    [ProtoMember(1)]
    public readonly string UpdatedCharacterId;

    public NetworkRefreshCharacter(string updatedCharacterId)
    {
        UpdatedCharacterId = updatedCharacterId;
    }
}
