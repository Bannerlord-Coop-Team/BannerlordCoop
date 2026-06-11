using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAttackMissionAttempted : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkAttackMissionAttempted(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
