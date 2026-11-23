using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventComponents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestLiftPlayerBlockade : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkRequestLiftPlayerBlockade(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
