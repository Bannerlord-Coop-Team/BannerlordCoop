using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// [Client to server] Reports that this client refused to open the field battle mission for a map event, so it will
/// not enter it. The server releases the sender's pending join for that battle instead of waiting for it to expire.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkFieldBattleOpenRefused : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkFieldBattleOpenRefused(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
