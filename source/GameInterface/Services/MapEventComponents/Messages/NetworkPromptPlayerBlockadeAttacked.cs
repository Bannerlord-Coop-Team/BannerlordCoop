using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventComponents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPromptPlayerBlockadeAttacked : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkPromptPlayerBlockadeAttacked(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
