#if DEBUG
using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace Missions.Naval;

/// <summary>DEBUG: one correction step for a steered copy: the velocity change to apply, or a snap to the owner's frame.</summary>
internal readonly struct CopySteeringStep
{
    public CopySteeringStep(bool snap, Vec3 deltaVelocity, float deltaYawRate, float alongHeading)
    {
        Snap = snap;
        DeltaVelocity = deltaVelocity;
        DeltaYawRate = deltaYawRate;
        AlongHeading = alongHeading;
    }

    /// <summary>The error is beyond the snap distance: teleport to the owner's frame and take its velocity.</summary>
    public bool Snap { get; }

    /// <summary>Planar velocity change in m/s (z is always 0).</summary>
    public Vec3 DeltaVelocity { get; }

    /// <summary>Yaw rate change in rad/s.</summary>
    public float DeltaYawRate { get; }

    /// <summary>The velocity change along the copy's heading, whose sign changes count as oscillations.</summary>
    public float AlongHeading { get; }
}

/// <summary>
/// DEBUG: the velocity correction that keeps a steered copy on its owner's samples. The copy's own physics, driven by the
/// owner's replayed helm input, is the feed-forward; this only closes the remaining planar position and heading error:
/// target velocity = owner velocity + clamp(gain * error / window), blended in with time constant tau. Inside the dead zone
/// nothing is applied, and heave, pitch and roll are left to the local water.
/// </summary>
internal sealed class CopySteering
{
    // Owner frames older than this are dropped; the owner's velocity is measured over at least MinVelocitySpan.
    internal const float OwnerHistorySeconds = 1f;
    internal const float MinVelocitySpan = 0.1f;
    // A correction along the heading smaller than this (m/s) has no sign, so noise does not count as oscillation.
    internal const float OscillationEpsilon = 0.01f;
    internal const float DegToRad = (float)(Math.PI / 180.0);

    private readonly List<(float Time, Vec3 Origin, float Yaw)> owner = new List<(float, Vec3, float)>();
    private int lastSign;
    private float sinceOwnerFrame;

    public CopySteering(CopySteeringKnobs knobs)
    {
        Knobs = knobs ?? new CopySteeringKnobs();
    }

    public CopySteeringKnobs Knobs { get; set; }

    public bool HasTarget => owner.Count > 0;

    public Vec3 TargetOrigin => owner.Count > 0 ? owner[owner.Count - 1].Origin : Vec3.Zero;

    public float TargetYaw => owner.Count > 0 ? owner[owner.Count - 1].Yaw : 0f;

    public long Oscillations { get; private set; }

    public long Snaps { get; private set; }

    /// <summary>Physics time stepped since the newest owner frame.</summary>
    public float SecondsSinceOwnerFrame => sinceOwnerFrame;

    /// <summary>The owner's frames stopped coming, so their last velocity no longer describes the target.</summary>
    public bool IsOwnerStale => sinceOwnerFrame > MinVelocitySpan;

    /// <summary>The owner velocity the correction adds, zero once the owner's frames are stale.</summary>
    public Vec3 FeedForwardVelocity => IsOwnerStale ? Vec3.Zero : OwnerVelocity;

    /// <summary>Takes the owner's frame this client would have written, at this client's mission time.</summary>
    public void ObserveOwner(float time, Vec3 origin, float yaw)
    {
        sinceOwnerFrame = 0f;
        owner.Add((time, origin, yaw));
        while (owner.Count > 2 && time - owner[0].Time > OwnerHistorySeconds)
            owner.RemoveAt(0);
    }

    /// <summary>The owner's planar velocity from its frames, or zero before they span <see cref="MinVelocitySpan"/>.</summary>
    public Vec3 OwnerVelocity
    {
        get
        {
            if (!TrySpan(out var oldest, out var newest, out float span)) return Vec3.Zero;
            var delta = (newest.Origin - oldest.Origin) / span;
            return new Vec3(delta.x, delta.y, 0f);
        }
    }

    /// <summary>The owner's yaw rate in rad/s from its frames, or zero before they span <see cref="MinVelocitySpan"/>.</summary>
    public float OwnerYawRate =>
        TrySpan(out var oldest, out var newest, out float span) ? WrapAngle(newest.Yaw - oldest.Yaw) / span : 0f;

    private bool TrySpan(out (float Time, Vec3 Origin, float Yaw) oldest, out (float Time, Vec3 Origin, float Yaw) newest,
        out float span)
    {
        oldest = default;
        newest = default;
        span = 0f;
        if (owner.Count < 2) return false;

        newest = owner[owner.Count - 1];
        for (int index = owner.Count - 2; index >= 0; index--)
        {
            if (newest.Time - owner[index].Time < MinVelocitySpan) continue;
            oldest = owner[index];
            span = newest.Time - oldest.Time;
            return true;
        }

        return false;
    }

    /// <summary>The correction for one physics step of <paramref name="dt"/> seconds from the copy's current state.</summary>
    public CopySteeringStep Compute(float dt, Vec3 origin, float yaw, Vec3 velocity, float yawRate)
    {
        if (!HasTarget || dt <= 0f || float.IsNaN(dt)) return default;

        sinceOwnerFrame += dt;
        bool ownerStale = IsOwnerStale;
        var knobs = Knobs;
        var target = TargetOrigin;
        var error = new Vec3(target.x - origin.x, target.y - origin.y, 0f);
        float distance = error.Length;
        if (distance > knobs.SnapMetres)
        {
            Snaps++;
            lastSign = 0;
            return new CopySteeringStep(true, Vec3.Zero, 0f, 0f);
        }

        float blend = Math.Min(1f, dt / knobs.TauSeconds);
        var deltaVelocity = Vec3.Zero;
        if (distance > knobs.DeadZoneMetres)
        {
            var correction = ClampLength(error * (knobs.Gain / knobs.WindowSeconds), knobs.ClampMetresPerSecond);
            var targetVelocity = (ownerStale ? Vec3.Zero : OwnerVelocity) + correction;
            deltaVelocity = new Vec3(targetVelocity.x - velocity.x, targetVelocity.y - velocity.y, 0f) * blend;
        }

        float deltaYawRate = 0f;
        float yawError = WrapAngle(TargetYaw - yaw);
        if (Math.Abs(yawError) > knobs.YawDeadZoneDegrees * DegToRad)
        {
            float yawClamp = knobs.YawClampDegreesPerSecond * DegToRad;
            float correction = Clamp(yawError * (knobs.Gain / knobs.WindowSeconds), -yawClamp, yawClamp);
            deltaYawRate = ((ownerStale ? 0f : OwnerYawRate) + correction - yawRate) * blend;
        }

        float along = (deltaVelocity.x * (float)Math.Cos(yaw)) + (deltaVelocity.y * (float)Math.Sin(yaw));
        CountOscillation(along);
        return new CopySteeringStep(false, deltaVelocity, deltaYawRate, along);
    }

    // A sign change of the correction along the heading; corrections inside the epsilon keep the last sign.
    private void CountOscillation(float along)
    {
        if (Math.Abs(along) < OscillationEpsilon) return;

        int sign = along > 0f ? 1 : -1;
        if (lastSign != 0 && sign != lastSign) Oscillations++;
        lastSign = sign;
    }

    internal static float WrapAngle(float radians)
    {
        const float TwoPi = (float)(2 * Math.PI);
        radians %= TwoPi;
        if (radians > Math.PI) radians -= TwoPi;
        else if (radians < -Math.PI) radians += TwoPi;
        return radians;
    }

    internal static Vec3 ClampLength(Vec3 vector, float max)
    {
        float length = vector.Length;
        return length > max && length > 0f ? vector * (max / length) : vector;
    }

    private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

    /// <summary>The yaw of a frame's forward axis in radians, counter-clockwise from +x.</summary>
    internal static float YawOf(Vec3 forward) => (float)Math.Atan2(forward.y, forward.x);
}
#endif
