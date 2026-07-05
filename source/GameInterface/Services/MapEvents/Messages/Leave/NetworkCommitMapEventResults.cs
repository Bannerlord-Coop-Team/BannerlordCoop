using Common.Messaging;
using GameInterface.Services.MapEvents.Data;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Leave;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCommitMapEventResults : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    [ProtoMember(2)]
    public readonly NetworkPlayerLootData PlayerLootData;

    public NetworkCommitMapEventResults(string mapEventId, NetworkPlayerLootData playerLootData)
    {
        MapEventId = mapEventId;
        PlayerLootData = playerLootData;
    }
}
