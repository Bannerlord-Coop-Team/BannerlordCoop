using ProtoBuf;
using System;
using System.Linq;

namespace Missions.Messages;

/// <summary>
/// A hull's damage state as its owner simulates it: campaign-ship hull and sail HP, mission fire HP, the six partial
/// (floater) HP values and the vanilla sinking state (0 floating, 1 sinking, 2 sunk).
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class BattleShipCondition
{
    public const int MaxPartialCount = 16;
    public const int Sunk = 2;
    private const float Tolerance = 0.01f;

    public BattleShipCondition(float hitPoints, float sailHitPoints, float fireHitPoints, float[] partialHitPoints, int sinkingState)
    {
        HitPoints = hitPoints;
        SailHitPoints = sailHitPoints;
        FireHitPoints = fireHitPoints;
        PartialHitPoints = partialHitPoints;
        SinkingState = sinkingState;
    }

    [ProtoMember(1)] public float HitPoints { get; private set; }
    [ProtoMember(2)] public float SailHitPoints { get; private set; }
    [ProtoMember(3)] public float FireHitPoints { get; private set; }
    [ProtoMember(4)] public float[] PartialHitPoints { get; private set; }
    [ProtoMember(5)] public int SinkingState { get; private set; }

    public bool IsValid => IsFinite(HitPoints) && IsFinite(SailHitPoints) && IsFinite(FireHitPoints)
        && (PartialHitPoints == null || (PartialHitPoints.Length <= MaxPartialCount && PartialHitPoints.All(IsFinite)))
        && SinkingState >= 0 && SinkingState <= Sunk;

    /// <summary>Whether peers holding <paramref name="previous"/> must be told about this condition.</summary>
    public bool DiffersFrom(BattleShipCondition previous)
    {
        if (previous == null || previous.SinkingState != SinkingState) return true;
        if (Differs(previous.HitPoints, HitPoints) || Differs(previous.SailHitPoints, SailHitPoints)
            || Differs(previous.FireHitPoints, FireHitPoints))
            return true;

        var partials = PartialHitPoints ?? Array.Empty<float>();
        var previousPartials = previous.PartialHitPoints ?? Array.Empty<float>();
        if (partials.Length != previousPartials.Length) return true;
        for (int i = 0; i < partials.Length; i++)
        {
            if (Differs(previousPartials[i], partials[i])) return true;
        }

        return false;
    }

    private static bool Differs(float a, float b) => Math.Abs(a - b) > Tolerance;

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
