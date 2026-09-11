#if DEBUG
using System;
using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabSailState
{
    [ProtoMember(1)] public Guid ShipId { get; private set; }
    [ProtoMember(2)] public int State { get; private set; }
    [ProtoMember(3)] public int Type { get; private set; }
    public bool IsValid => ShipId != Guid.Empty && State >= 0 && State <= 2 && Type >= 0 && Type <= 2;
    public NetworkNavalLabSailState(Guid shipId, int state, int type)
    { ShipId = shipId; State = state; Type = type; }
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabStations : IEvent
{
    [ProtoMember(1)] public Guid IncarnationId { get; private set; }
    [ProtoMember(2)] public int Epoch { get; private set; }
    [ProtoMember(3)] public int Ship { get; private set; }
    [ProtoMember(4)] public string Phase { get; private set; }
    [ProtoMember(5)] public Guid[] Combatants { get; private set; }
    [ProtoMember(6)] public string[] Keys { get; private set; }
    public NetworkNavalLabStations(Guid incarnation, int epoch, int ship, string phase, Guid[] combatants, string[] keys)
    {
        IncarnationId = incarnation; Epoch = epoch; Ship = ship; Phase = phase;
        Combatants = (Guid[])combatants.Clone(); Keys = (string[])keys.Clone();
    }
    public NetworkNavalLabStations WithPhase(string phase) => new(IncarnationId, Epoch, Ship, phase, Combatants, Keys);
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabHelmInput : IEvent
{
    [ProtoMember(1)] public Guid IncarnationId { get; private set; }
    [ProtoMember(2)] public int Epoch { get; private set; }
    [ProtoMember(3)] public int Ship { get; private set; }
    [ProtoMember(4)] public long Sequence { get; private set; }
    [ProtoMember(5)] public long DeadlineUtcTicks { get; private set; }
    [ProtoMember(6)] public int Lateral { get; private set; }
    [ProtoMember(7)] public int Longitudinal { get; private set; }
    [ProtoMember(8)] public int DoubleTap { get; private set; }
    [ProtoMember(9)] public float Rudder { get; private set; }
    [ProtoMember(10)] public int Sail { get; private set; }
    [ProtoMember(11)] public bool HasHelm { get; private set; }
    public bool IsValid => Lateral >= -1 && Lateral <= 2 && Longitudinal >= -1 && Longitudinal <= 2
        && DoubleTap >= -1 && DoubleTap <= 2 && !float.IsNaN(Rudder) && !float.IsInfinity(Rudder)
        && Math.Abs(Rudder) <= 1 && Sail >= 0 && Sail <= 2;
    public NetworkNavalLabHelmInput(Guid incarnation, int epoch, int ship, long sequence, long deadline,
        bool hasHelm, int lateral, int longitudinal, int doubleTap, float rudder, int sail)
    {
        IncarnationId = incarnation; Epoch = epoch; Ship = ship; Sequence = sequence; DeadlineUtcTicks = deadline;
        HasHelm = hasHelm; Lateral = lateral; Longitudinal = longitudinal; DoubleTap = doubleTap; Rudder = rudder; Sail = sail;
    }
}
#endif
