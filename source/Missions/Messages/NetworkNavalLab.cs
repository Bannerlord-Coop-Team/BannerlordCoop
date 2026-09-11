#if DEBUG
using Common.Messaging;
using Missions.Battles;
using ProtoBuf;
using System;

namespace Missions.Messages;

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabStart : IEvent
{
    [ProtoMember(1)] public readonly string InstanceId;
    [ProtoMember(2)] public readonly Guid IncarnationId;
    [ProtoMember(3)] public readonly string[] Controllers;
    [ProtoMember(4)] public readonly Guid[] Combatants;
    [ProtoMember(5)] public readonly Guid[] Ships;
    [ProtoMember(6)] public readonly NavalLabMode Mode;
    public NetworkNavalLabStart(string instanceId, Guid incarnationId, string[] controllers, Guid[] combatants, Guid[] ships,
        NavalLabMode mode = NavalLabMode.Activation)
    {
        Mode = mode;
        InstanceId = instanceId;
        IncarnationId = incarnationId;
        Controllers = controllers;
        Combatants = combatants;
        Ships = ships;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabFrames : IEvent
{
    [ProtoMember(1)] public readonly Guid IncarnationId;
    [ProtoMember(2)] public readonly int Epoch;
    [ProtoMember(3)] public readonly long Sequence;
    [ProtoMember(4)] public readonly float[] Frames;
    [ProtoMember(5)] public readonly long SourceCallback;
    [ProtoMember(6)] public readonly Guid ProbeOperationId;
    [ProtoMember(7)] public readonly NetworkNavalLabSailState[] SailStates;
    [ProtoMember(8)] public readonly long SailDeadlineUtcTicks;
    [ProtoMember(9)] public readonly NetworkNavalLabPresentation[] Presentation;
    public NetworkNavalLabFrames(Guid incarnationId, int epoch, long sequence, float[] frames,
        long sourceCallback = 0, Guid probeOperationId = default,
        NetworkNavalLabSailState[] sailStates = null, long sailDeadlineUtcTicks = 0, NetworkNavalLabPresentation[] presentation = null)
    {
        IncarnationId = incarnationId;
        Epoch = epoch;
        Sequence = sequence;
        Frames = frames;
        SourceCallback = sourceCallback;
        ProbeOperationId = probeOperationId;
        SailStates = sailStates;
        SailDeadlineUtcTicks = sailDeadlineUtcTicks;
        Presentation = presentation;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabAction : IEvent
{
    [ProtoMember(1)] public readonly Guid IncarnationId;
    [ProtoMember(2)] public readonly Guid OperationId;
    [ProtoMember(3)] public readonly int Epoch;
    [ProtoMember(4)] public readonly string Kind;
    [ProtoMember(5)] public readonly int Ship;
    [ProtoMember(6)] public readonly float Rudder;
    [ProtoMember(7)] public readonly bool Row;
    [ProtoMember(8)] public readonly long DeadlineUtcTicks;
    public NetworkNavalLabAction(Guid incarnationId, Guid operationId, int epoch, string kind, int ship, float rudder, bool row,
        long deadlineUtcTicks = 0)
    {
        IncarnationId = incarnationId;
        OperationId = operationId;
        Epoch = epoch;
        DeadlineUtcTicks = deadlineUtcTicks;
        Kind = kind;
        Ship = ship;
        Rudder = rudder;
        Row = row;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabReceipt : IEvent
{
    [ProtoMember(1)] public readonly Guid IncarnationId;
    [ProtoMember(2)] public readonly Guid OperationId;
    [ProtoMember(3)] public readonly string Status;
    public NetworkNavalLabReceipt(Guid incarnationId, Guid operationId, string status)
    {
        IncarnationId = incarnationId;
        OperationId = operationId;
        Status = status;
    }
}
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabFault : IEvent
{
    [ProtoMember(1)] public readonly Guid IncarnationId;
    [ProtoMember(2)] public readonly string Reason;
    public NetworkNavalLabFault(Guid incarnationId, string reason)
    {
        IncarnationId = incarnationId;
        Reason = reason;
    }
}
#endif
