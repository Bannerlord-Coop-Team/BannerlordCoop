#if DEBUG
using System;
using System.Linq;
using ProtoBuf;

namespace Missions.Messages;

// One source station owns its connection lifecycle; hull authority remains per original owner.
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabRopeState
{
    // The drakkar prefab has 40 attachment targets, separate from its 12 throw stations.
    public const int MaxTargetStations = 64;

    [ProtoMember(1)] public int SourceStation;
    [ProtoMember(2)] public string SourceKey;
    [ProtoMember(3)] public int TargetStation;
    [ProtoMember(4)] public string TargetKey;
    [ProtoMember(5)] public long Generation;
    [ProtoMember(6)] public int State;
    [ProtoMember(7)] public float Length;
    [ProtoMember(8)] public float[] HookFrame;
    [ProtoMember(9)] public int[] History;
    [ProtoMember(10)] public Guid OperationId;
    [ProtoMember(11)] public float[] CurveTarget;
    [ProtoMember(12)] public float CurveAngle;

    public bool IsValid => SourceStation >= 0 && SourceStation < 32 && !string.IsNullOrEmpty(SourceKey)
        && TargetStation >= -1 && TargetStation < MaxTargetStations && (TargetStation == -1 || !string.IsNullOrEmpty(TargetKey))
        && Generation > 0 && (State == 0 || State == 1 || State == 4 || State == 5)
        && !float.IsNaN(Length) && !float.IsInfinity(Length) && Length >= 0 && Length <= 200
        && NetworkNavalLabOarPresentation.ValidFrame(HookFrame)
        && (CurveTarget == null || (CurveTarget.Length == 3 && CurveTarget.All(value => NetworkNavalLabPresentation.Bounded(value, -1000, 1000))
            && NetworkNavalLabPresentation.Bounded(CurveAngle, -180, 180)))
        && History != null && History.Length <= 8 && History.All(state => state == 0 || state == 1 || state == 4 || state == 5);
}
#endif
