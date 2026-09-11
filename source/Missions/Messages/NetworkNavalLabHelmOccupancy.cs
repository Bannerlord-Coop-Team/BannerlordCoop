#if DEBUG
using System;
using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabHelmOccupancy : IEvent
{
    [ProtoMember(1)] public Guid IncarnationId { get; private set; }
    [ProtoMember(2)] public int Epoch { get; private set; }
    [ProtoMember(3)] public int Ship { get; private set; }
    [ProtoMember(4)] public Guid ShipId { get; private set; }
    [ProtoMember(5)] public Guid CombatantId { get; private set; }
    [ProtoMember(6)] public string Key { get; private set; }
    [ProtoMember(7)] public long Revision { get; private set; }
    [ProtoMember(8)] public bool Occupied { get; private set; }
    [ProtoMember(9)] public string Phase { get; private set; }

    public NetworkNavalLabHelmOccupancy(Guid incarnationId, int epoch, int ship, Guid shipId,
        Guid combatantId, string key, long revision, bool occupied, string phase)
    {
        IncarnationId = incarnationId; Epoch = epoch; Ship = ship; ShipId = shipId;
        CombatantId = combatantId; Key = key; Revision = revision; Occupied = occupied; Phase = phase;
    }

    public NetworkNavalLabHelmOccupancy WithPhase(string phase) => new(IncarnationId, Epoch, Ship,
        ShipId, CombatantId, Key, Revision, Occupied, phase);

    public bool SameState(NetworkNavalLabHelmOccupancy other) => other != null
        && IncarnationId == other.IncarnationId && Epoch == other.Epoch && Ship == other.Ship
        && ShipId == other.ShipId && CombatantId == other.CombatantId && Key == other.Key
        && Revision == other.Revision && Occupied == other.Occupied;
}
#endif
