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
    [ProtoMember(3)]
    public readonly int HostEpoch;

    public NetworkMapEventSurrender(string mapEventId, BattleSideEnum side, int hostEpoch = 0)
    {
        MapEventId = mapEventId;
        Side = side;
        HostEpoch = hostEpoch;
    }
}
