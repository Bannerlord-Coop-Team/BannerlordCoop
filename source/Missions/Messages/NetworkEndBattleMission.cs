using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// [Server to client] The server finalized this map event (a peace decision, say) while the receiving player was
/// still in or joining its battle mission, so the player leaves that mission and no result is applied.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkEndBattleMission : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkEndBattleMission(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
