using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Client → server: ask for the reserves this client currently OWNS. Sent at battle ENTRY (while still
/// loading, so its own party's troops feed the suppliers during the scene load — the unowned/NPC sides are
/// decided later, by the mission-ready election), and by a client that has just taken over a departed
/// owner's troops (a host adopting a disconnected player, or a successor promoted to host) to ask for its
/// now-larger owned reserve. The server replies with the owned set at the current ledger pointers (empty
/// sides skipped), so a new owner resumes the adopted parties' reinforcements exactly where the departed
/// owner left off.
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
