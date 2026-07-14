using System;
using TaleWorlds.Library;

namespace Missions.Missiles.Handlers;

internal readonly struct MissileReplayPlan
{
    public Vec3 Position { get; }
    public Vec3 Direction { get; }
    public Mat3 Orientation { get; }
    public float Speed { get; }
    public float RemainingFlightSeconds { get; }
    public bool IsFastForwarded { get; }

    public MissileReplayPlan(Vec3 position, Vec3 direction, Mat3 orientation, float speed,
        float remainingFlightSeconds, bool isFastForwarded)
    {
        Position = position;
        Direction = direction;
        Orientation = orientation;
        Speed = speed;
        RemainingFlightSeconds = remainingFlightSeconds;
        IsFastForwarded = isFastForwarded;
    }
}

/// <summary>Plans either the original launch or a short replay ending at the victim's current position.</summary>
internal static class MissileReplayPlanner
{
    internal const float FinalSegmentSeconds = 0.1f;
    internal const float MaximumFinalSegmentDistance = 4f;

    public static MissileReplayPlan Plan(Vec3 position, Vec3 direction, Mat3 orientation, float speed,
        bool fastForward, Vec3 currentTarget, Vec3 impactVelocity)
    {
        if (!fastForward || !TryNormalize(currentTarget - position, out Vec3 toTarget, out float targetDistance))
            return new MissileReplayPlan(position, direction, orientation, speed, 0f, false);

        Vec3 replayDirection;
        float replaySpeed;
        if (!TryNormalize(impactVelocity, out replayDirection, out replaySpeed))
        {
            if (!TryNormalize(direction, out replayDirection, out _))
                return new MissileReplayPlan(position, direction, orientation, speed, 0f, false);

            replaySpeed = IsFinite(speed) && speed > 1f ? speed : 1f;
        }

        float segmentDistance = Math.Min(targetDistance,
            Math.Min(MaximumFinalSegmentDistance, replaySpeed * FinalSegmentSeconds));
        if (segmentDistance >= targetDistance)
            replayDirection = toTarget;

        position = currentTarget - (replayDirection * segmentDistance);
        orientation.f = replayDirection;
        orientation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
        return new MissileReplayPlan(position, replayDirection, orientation, replaySpeed,
            segmentDistance / replaySpeed, true);
    }

    internal static bool IsFinite(Vec3 value) =>
        IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);

    private static bool TryNormalize(Vec3 value, out Vec3 normalized, out float length)
    {
        normalized = default;
        length = 0f;
        if (!IsFinite(value) || value.LengthSquared <= 0.000001f)
            return false;

        length = (float)Math.Sqrt(value.LengthSquared);
        normalized = value * (1f / length);
        return IsFinite(normalized);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
