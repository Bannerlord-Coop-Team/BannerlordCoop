using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Client → server: sent by a client that has just taken over a departed owner's troops (a host adopting a
/// disconnected player, or a successor promoted to host) to ask the server for its now-larger owned reserve.
/// The server replies with the full owned set at the current ledger pointers, so the new owner resumes the
/// adopted parties' reinforcements exactly where the departed owner left off.
/// </summary>
[ProtoContract]
public readonly struct NetworkRequestBattleReserves : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly string ControllerId;

    public NetworkRequestBattleReserves(string mapEventId, string controllerId)
    {
        MapEventId = mapEventId;
        ControllerId = controllerId;
    }
}
