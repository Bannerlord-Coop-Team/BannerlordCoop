#if DEBUG
using System;
using System.Linq;
using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

// The owner's frozen rope lifecycle at terminal hold; ship samples stop before it can otherwise reach the other client.
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabRopeFinal : IEvent
{
    [ProtoMember(1)] public Guid IncarnationId { get; private set; }
    [ProtoMember(2)] public int Slot { get; private set; }
    [ProtoMember(3)] public Guid ShipId { get; private set; }
    [ProtoMember(4)] public NetworkNavalLabRopeState[] Ropes { get; private set; }

    public bool IsValid => IncarnationId != Guid.Empty && Slot >= 0 && Slot < 2 && ShipId != Guid.Empty
        && Ropes != null && Ropes.Length > 0 && Ropes.Length <= 32 && Ropes.All(rope => rope?.IsValid == true)
        && Ropes.Select(rope => rope.SourceStation).Distinct().Count() == Ropes.Length;

    public NetworkNavalLabRopeFinal(Guid incarnationId, int slot, Guid shipId, NetworkNavalLabRopeState[] ropes)
    {
        IncarnationId = incarnationId; Slot = slot; ShipId = shipId; Ropes = ropes;
    }
}
#endif
