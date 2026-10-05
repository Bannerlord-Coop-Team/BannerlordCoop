using ProtoBuf;
using System;
using System.Linq;

namespace Missions.Messages;

/// <summary>
/// The owner's rope at one throw station of its hull, keyed by the station's content path. Every hull sample
/// carries the whole set; a station's generation only grows and its removal is repeated until the next throw,
/// so a dropped or rejected sample is repaired by the next one.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class BattleRopeState
{
    public const int MaxRopesPerHull = 32;

    // Vanilla ShipAttachment.ShipAttachmentState values.
    public const int RopeThrown = 0;
    public const int RopesPulling = 1;
    public const int BridgeThrown = 2;
    public const int BridgeConnected = 3;
    public const int Removed = 4;
    public const int RopeFailedAndReloading = 5;

    /// <summary>The throw station's content path below the source hull.</summary>
    [ProtoMember(1)] public string SourceKey;
    /// <summary>Grows each time the owner creates a new attachment at this station.</summary>
    [ProtoMember(2)] public long Generation;
    [ProtoMember(3)] public int State;
    /// <summary>The hull the hook landed on, Guid.Empty while the rope flies or after it missed.</summary>
    [ProtoMember(4)] public Guid TargetShipId;
    /// <summary>The attachment point's content path below the target hull.</summary>
    [ProtoMember(5)] public string TargetKey;
    [ProtoMember(6)] public float Length;
    [ProtoMember(7)] public float[] HookFrame;
    /// <summary>The owner's rope curve target and throw angle this frame while the rope flies, else null.</summary>
    [ProtoMember(8)] public float[] CurveTarget;
    [ProtoMember(9)] public float CurveAngle;
    /// <summary>The plank's flight clock while thrown or connected (8 values), else null.</summary>
    [ProtoMember(10)] public float[] PlankFlight;
    /// <summary>The owner's plank decoration count, so a replica lays the same plank ropes.</summary>
    [ProtoMember(11)] public int DecorationPlanks;

    public bool HasTarget => TargetShipId != Guid.Empty;

    public bool IsPlank => State == BridgeThrown || State == BridgeConnected;

    public bool IsValid => !string.IsNullOrEmpty(SourceKey) && Generation > 0 && State >= RopeThrown && State <= RopeFailedAndReloading
        && HasTarget == !string.IsNullOrEmpty(TargetKey)
        && (State != RopesPulling || HasTarget)
        && Bounded(Length, 0f, 200f)
        && NetworkBattleShipSample.IsValidFrame(HookFrame)
        && (CurveTarget == null || (CurveTarget.Length == 3 && CurveTarget.All(value => Bounded(value, -10000f, 10000f))
            && Bounded(CurveAngle, -180f, 180f)))
        && (!IsPlank || (HasTarget && PlankFlight?.Length == 8 && PlankFlight.All(value => Bounded(value, -10000f, 10000f))
            && PlankFlight[3] > 0f))
        && DecorationPlanks >= 0 && DecorationPlanks <= 80;

    /// <summary>A hull's rope set: bounded, each state valid and one state per station. Null is an empty set.</summary>
    public static bool AreValid(BattleRopeState[] ropes) => ropes == null
        || (ropes.Length <= MaxRopesPerHull && ropes.All(rope => rope?.IsValid == true)
            && ropes.Select(rope => rope.SourceKey).Distinct().Count() == ropes.Length);

    private static bool Bounded(float value, float min, float max) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
}
