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

/// <summary>
/// Produces native-safe launch data for either an ordinary replicated missile or a short terminal replay when
/// the matching impact reached this client before the missile could be reconstructed.
/// </summary>
internal static class MissileReplayPlanner
{
    internal const float FinalSegmentSeconds = 0.1f;
    internal const float MaximumFinalSegmentDistance = 4f;

    private const float MinimumUsableSpeed = 1f;
    private const double MinimumDirectionLengthSquared = 0.000001d;

    public static MissileReplayPlan Plan(Vec3 originalPosition, Vec3 originalDirection, Mat3 originalOrientation,
        float originalSpeed, bool fastForward, Vec3 currentImpactTarget, Vec3 impactVelocity)
    {
        Vec3 safePosition = IsFinite(originalPosition) ? originalPosition : Vec3.Zero;
        bool hasOriginalDirection = TryNormalize(originalDirection, out Vec3 normalizedOriginal, out _);
        Vec3 safeDirection = hasOriginalDirection ? originalDirection : new Vec3(0f, 1f, 0f);
        Vec3 normalizedSafeDirection = hasOriginalDirection ? normalizedOriginal : safeDirection;
        float safeSpeed = IsUsableSpeed(originalSpeed) ? originalSpeed : MinimumUsableSpeed;
        Mat3 safeOrientation = IsFinite(originalOrientation)
            ? originalOrientation
            : CreateOrientation(normalizedSafeDirection);

        if (!fastForward || !IsFinite(currentImpactTarget))
            return new MissileReplayPlan(safePosition, safeDirection, safeOrientation, safeSpeed, 0f, false);

        Vec3 finalDirection = normalizedSafeDirection;
        float finalSpeed = safeSpeed;
        if (TryNormalize(impactVelocity, out Vec3 normalizedImpact, out float impactSpeed)
            && IsUsableSpeed(impactSpeed))
        {
            finalDirection = normalizedImpact;
            finalSpeed = impactSpeed;
        }

        Vec3 fromOrigin = currentImpactTarget - safePosition;
        bool hasOriginToTarget = TryNormalize(fromOrigin, out Vec3 originToTarget, out float originToTargetDistance);
        float desiredDistance = Math.Min(finalSpeed * FinalSegmentSeconds, MaximumFinalSegmentDistance);
        float segmentDistance = hasOriginToTarget
            ? Math.Min(desiredDistance, originToTargetDistance)
            : 0f;

        // For a target already inside the terminal-segment window, begin at the original launch rather than
        // extending the replay behind the shooter.
        if (hasOriginToTarget && originToTargetDistance <= desiredDistance)
            finalDirection = originToTarget;

        Vec3 finalPosition = currentImpactTarget - finalDirection * segmentDistance;
        if (!IsFinite(finalPosition))
            return new MissileReplayPlan(safePosition, safeDirection, safeOrientation, safeSpeed, 0f, false);

        float remainingFlightSeconds = segmentDistance / finalSpeed;
        if (!IsFinite(remainingFlightSeconds) || remainingFlightSeconds < 0f)
            remainingFlightSeconds = 0f;

        return new MissileReplayPlan(finalPosition, finalDirection, CreateOrientation(finalDirection), finalSpeed,
            remainingFlightSeconds, true);
    }

    internal static bool IsFinite(Vec3 value) =>
        IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);

    private static bool IsFinite(Mat3 value) =>
        IsFinite(value.s) && IsFinite(value.f) && IsFinite(value.u);

    private static bool TryNormalize(Vec3 value, out Vec3 normalized, out float length)
    {
        normalized = default;
        length = 0f;
        if (!IsFinite(value))
            return false;

        double lengthSquared = (double)value.X * value.X
            + (double)value.Y * value.Y
            + (double)value.Z * value.Z;
        if (!IsFinite(lengthSquared) || lengthSquared <= MinimumDirectionLengthSquared)
            return false;

        double preciseLength = Math.Sqrt(lengthSquared);
        if (!IsFinite(preciseLength) || preciseLength <= 0d || preciseLength > float.MaxValue)
            return false;

        length = (float)preciseLength;
        float inverseLength = 1f / length;
        normalized = new Vec3(value.X * inverseLength, value.Y * inverseLength, value.Z * inverseLength);
        return IsFinite(normalized);
    }

    private static Mat3 CreateOrientation(Vec3 forward)
    {
        Vec3 side = Math.Abs(forward.Z) < 0.999f
            ? new Vec3(forward.Y, -forward.X, 0f)
            : new Vec3(1f, 0f, 0f);
        TryNormalize(side, out side, out _);

        Vec3 up = new Vec3(
            side.Y * forward.Z - side.Z * forward.Y,
            side.Z * forward.X - side.X * forward.Z,
            side.X * forward.Y - side.Y * forward.X);
        TryNormalize(up, out up, out _);
        return new Mat3(side, forward, up);
    }

    private static bool IsUsableSpeed(float value) => IsFinite(value) && value >= MinimumUsableSpeed;

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
