using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEventSides.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAssignMapEventSide : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly string MapEventSideId;
    [ProtoMember(3)]
    public readonly BattleSideEnum Side;

    public NetworkAssignMapEventSide(string mapEventId, string mapEventSideId, BattleSideEnum side)
    {
        MapEventId = mapEventId;
        MapEventSideId = mapEventSideId;
        Side = side;
    }
}
