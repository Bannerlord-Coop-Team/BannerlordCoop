#if DEBUG
using System;
using System.Linq;
using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabShipSample : IEvent
{
    [ProtoMember(1)] public readonly string InstanceId;
    [ProtoMember(2)] public readonly Guid IncarnationId;
    [ProtoMember(3)] public readonly int Slot;
    [ProtoMember(4)] public readonly Guid ShipId;
    [ProtoMember(5)] public readonly string OriginalOwner;
    [ProtoMember(6)] public readonly int AuthorityRevision;
    [ProtoMember(7)] public readonly long Sequence;
    [ProtoMember(8)] public readonly long SourceCallback;
    [ProtoMember(9)] public readonly float[] Frame;
    [ProtoMember(10)] public readonly NetworkNavalLabPresentation Presentation;
    [ProtoMember(11)] public readonly NetworkNavalLabSailState SailState;
    [ProtoMember(12)] public readonly long DeadlineUtcTicks;
    [ProtoMember(13)] public readonly NetworkNavalLabRopeState[] Ropes;

    public NetworkNavalLabShipSample(string instanceId, Guid incarnationId, int slot, Guid shipId, string originalOwner,
        long sequence, long sourceCallback, float[] frame, NetworkNavalLabPresentation presentation, NetworkNavalLabSailState sailState,
        NetworkNavalLabRopeState[] ropes = null)
    {
        InstanceId = instanceId; IncarnationId = incarnationId; Slot = slot; ShipId = shipId; OriginalOwner = originalOwner;
        AuthorityRevision = 1; Sequence = sequence; SourceCallback = sourceCallback; Frame = frame;
        Presentation = presentation; SailState = sailState; DeadlineUtcTicks = DateTime.UtcNow.AddSeconds(1).Ticks;
        Ropes = ropes;
    }

    public bool IsValid => AuthorityRevision == 1 && Sequence > 0 && SourceCallback > 0
        && NetworkNavalLabOarPresentation.ValidFrame(Frame)
        && Presentation?.IsValid == true && Presentation.ShipId == ShipId && Presentation.Sails.All(sail => sail.Type == 0)
        && SailState?.IsValid == true && SailState.ShipId == ShipId && SailState.Type == 0
        && (Ropes == null || (Ropes.Length <= 32 && Ropes.All(rope => rope?.IsValid == true)
            && Ropes.Select(rope => rope.SourceStation).Distinct().Count() == Ropes.Length));
}
#endif
