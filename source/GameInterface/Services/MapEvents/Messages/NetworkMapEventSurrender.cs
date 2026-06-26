using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// [Client -> Server] Tells the server which side surrendered in a map event so it runs the
/// surrender authoritatively (marking the side as surrendered) before the server-side capture. The
/// capture then takes the full surrendered prisoner count instead of the reduced battle rate.
/// </summary>
[ProtoContract]
public readonly struct NetworkMapEventSurrender : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly BattleSideEnum Side;

    public NetworkMapEventSurrender(string mapEventId, BattleSideEnum side)
    {
        MapEventId = mapEventId;
        Side = side;
    }
}
