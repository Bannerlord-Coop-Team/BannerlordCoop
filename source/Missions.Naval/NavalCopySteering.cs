using System;
using System.Globalization;
using TaleWorlds.Library;

namespace Missions.Naval;

/// <summary>The steered copies' correction knobs; production defaults, which DEBUG <c>coop.debug.naval.copy_steer</c> tunes live.</summary>
public sealed class CopySteeringKnobs
{
    internal const string KnobNames = "gain window softzone clamp tau lead snap";

    /// <summary>Share of the position error the target velocity closes per <see cref="WindowSeconds"/>.</summary>
    public float Gain { get; private set; } = 1f;

    /// <summary>Seconds over which the target velocity closes the position and heading error.</summary>
    public float WindowSeconds { get; private set; } = 0.5f;

    /// <summary>Planar error in metres over which the correction gain ramps smoothly from zero to full.</summary>
    public float SoftZoneMetres { get; private set; } = 0.5f;

    /// <summary>Largest correction speed in m/s added on top of the owner's velocity.</summary>
    public float ClampMetresPerSecond { get; private set; } = 4f;

    /// <summary>Time constant in seconds over which the body's velocity is blended to the target velocity.</summary>
    public float TauSeconds { get; private set; } = 0.2f;

    /// <summary>Seconds the target is extrapolated past the sample's age, covering the owner-to-here delay.</summary>
    public float LeadSeconds { get; private set; } = 0.1f;

    /// <summary>Planar error or sample jump in metres beyond which the copy is teleported to the owner's frame.</summary>
    public float SnapMetres { get; private set; } = 25f;

    /// <summary>Applies one knob; why it was refused, or null.</summary>
    public string TryApply(string key, string value)
    {
        switch (key?.Trim().ToLowerInvariant())
        {
            case "gain": return Set(value, 0f, 10f, "gain", v => Gain = v);
            case "window": return Set(value, 0.05f, 5f, "window (s)", v => WindowSeconds = v);
            case "softzone": return Set(value, 0f, 10f, "softzone (m)", v => SoftZoneMetres = v);
            case "clamp": return Set(value, 0f, 30f, "clamp (m/s)", v => ClampMetresPerSecond = v);
            case "tau": return Set(value, 0.01f, 5f, "tau (s)", v => TauSeconds = v);
            case "lead": return Set(value, 0f, 1f, "lead (s)", v => LeadSeconds = v);
            case "snap": return Set(value, 1f, 500f, "snap (m)", v => SnapMetres = v);
            default: return $"Unknown knob {key}; knobs: {KnobNames}.";
        }
    }

    // Parses and range-checks one knob value, then assigns it; the refusal text, or null.
    private static string Set(string value, float min, float max, string name, Action<float> assign)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) || float.IsNaN(parsed)
            || parsed < min || parsed > max)
            return $"{name} must be a number from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}.";

        assign(parsed);
        return null;
    }

    public override string ToString() => string.Format(CultureInfo.InvariantCulture,
        "gain={0} window={1} softzone={2} clamp={3} tau={4} lead={5} snap={6}",
        Gain, WindowSeconds, SoftZoneMetres, ClampMetresPerSecond, TauSeconds, LeadSeconds, SnapMetres);
}

/// <summary>One correction step for a steered copy: the velocity change to apply, or a snap to the owner's frame.</summary>
internal readonly struct CopySteeringStep
{
    public CopySteeringStep(bool snap, MatrixFrame snapFrame, Vec2 deltaVelocity, float deltaYawRate, float alongHeading)
    {
        Snap = snap;
        SnapFrame = snapFrame;
        DeltaVelocity = deltaVelocity;
        DeltaYawRate = deltaYawRate;
        AlongHeading = alongHeading;
    }

    /// <summary>Teleport to <see cref="SnapFrame"/> and take the owner's velocity instead of steering.</summary>
    public bool Snap { get; }

    public MatrixFrame SnapFrame { get; }

    /// <summary>Planar velocity change in m/s.</summary>
    public Vec2 DeltaVelocity { get; }

    /// <summary>Yaw rate change in rad/s.</summary>
    public float DeltaYawRate { get; }

    /// <summary>The velocity change along the copy's heading, whose sign changes count as oscillations.</summary>
    public float AlongHeading { get; }
}

/// <summary>
/// Steers one copy toward its owner's latest sample extrapolated by the owner's velocity: target velocity = owner velocity
/// + clamp(gain * soft-zoned error / window), blended in with time constant tau. Heave, pitch and roll stay with the local water.
/// </summary>
internal sealed class CopySteering
{
    // A sample is extrapolated at most this far; older than StaleSeconds its velocity no longer feeds forward.
    internal const float MaxExtrapolationSeconds = 0.3f;
    internal const float StaleSeconds = 1f;
    // The yaw correction ramps in between these heading errors, as the position correction does over the soft zone.
    internal const float YawSoftStartDegrees = 1f;
    internal const float YawSoftEndDegrees = 3f;
    internal const float YawClampDegreesPerSecond = 10f;
    // A correction along the heading smaller than this (m/s) has no sign, so noise does not count as oscillation.
    internal const float OscillationEpsilon = 0.01f;
    internal const float DegToRad = (float)(Math.PI / 180.0);

    private readonly CopySteeringKnobs knobs;
    private MatrixFrame sampleFrame;
    private Vec2 sampleVelocity;
    private float sampleYawRate;
    private bool pendingSnap;
    private int lastSign;

    public CopySteering(CopySteeringKnobs knobs)
    {
        this.knobs = knobs ?? new CopySteeringKnobs();
    }

    public bool HasTarget { get; private set; }

    /// <summary>The owner's latest sample frame as received, not extrapolated.</summary>
    public MatrixFrame SampleFrame => sampleFrame;

    /// <summary>Physics time stepped since the latest sample arrived.</summary>
    public float SecondsSinceSample { get; private set; }

    /// <summary>The owner's samples stopped coming, so their velocity no longer feeds forward.</summary>
    public bool IsOwnerStale => SecondsSinceSample > StaleSeconds;

    public long Oscillations { get; private set; }

    public long Snaps { get; private set; }

    /// <summary>Takes the owner's latest sample; a jump past the snap distance from where the last one led snaps the copy.</summary>
    public void ObserveOwner(MatrixFrame frame, Vec2 velocity, float yawRate)
    {
        if (HasTarget && (frame.origin.AsVec2 - Extrapolate(StaleSeconds).Position).Length > knobs.SnapMetres) pendingSnap = true;

        sampleFrame = frame;
        sampleVelocity = velocity;
        sampleYawRate = yawRate;
        SecondsSinceSample = 0f;
        HasTarget = true;
    }

    /// <summary>The planar position and yaw the copy steers toward right now.</summary>
    internal (Vec2 Position, float Yaw) Target => Extrapolate(MaxExtrapolationSeconds);

    /// <summary>The correction for one physics step of <paramref name="dt"/> seconds from the copy's current state.</summary>
    public CopySteeringStep Compute(float dt, Vec3 origin, float yaw, Vec2 velocity, float yawRate)
    {
        if (!HasTarget || !(dt > 0f) || float.IsInfinity(dt)) return default;

        SecondsSinceSample += dt;
        var target = Target;
        var error = target.Position - origin.AsVec2;
        float distance = error.Length;
        if (pendingSnap || distance > knobs.SnapMetres) return SnapTo(target.Position);

        bool stale = IsOwnerStale;
        float rate = knobs.Gain / knobs.WindowSeconds;
        float blend = Math.Min(1f, dt / knobs.TauSeconds);
        var correction = ClampLength(error * (rate * SmoothStep(0f, knobs.SoftZoneMetres, distance)), knobs.ClampMetresPerSecond);
        var deltaVelocity = ((stale ? Vec2.Zero : sampleVelocity) + correction - velocity) * blend;

        float yawError = WrapAngle(target.Yaw - yaw);
        float yawGain = rate * SmoothStep(YawSoftStartDegrees * DegToRad, YawSoftEndDegrees * DegToRad, Math.Abs(yawError));
        float yawClamp = YawClampDegreesPerSecond * DegToRad;
        float yawCorrection = Clamp(yawError * yawGain, -yawClamp, yawClamp);
        float deltaYawRate = ((stale ? 0f : sampleYawRate) + yawCorrection - yawRate) * blend;

        float along = (deltaVelocity.x * (float)Math.Cos(yaw)) + (deltaVelocity.y * (float)Math.Sin(yaw));
        CountOscillation(along);
        return new CopySteeringStep(false, default, deltaVelocity, deltaYawRate, along);
    }

    // The sample moved on by its velocity for its age, capped at maxAge, plus the lead.
    private (Vec2 Position, float Yaw) Extrapolate(float maxAge)
    {
        float ahead = Math.Min(SecondsSinceSample, maxAge) + knobs.LeadSeconds;
        return (sampleFrame.origin.AsVec2 + (sampleVelocity * ahead), YawOf(sampleFrame.rotation.f) + (sampleYawRate * ahead));
    }

    // The owner's frame moved to the extrapolated target, which the engine teleports the copy to.
    private CopySteeringStep SnapTo(Vec2 position)
    {
        pendingSnap = false;
        Snaps++;
        lastSign = 0;
        var frame = sampleFrame;
        frame.origin = new Vec3(position.x, position.y, frame.origin.z);
        return new CopySteeringStep(true, frame, Vec2.Zero, 0f, 0f);
    }

    /// <summary>The owner's velocity the snap carries over, zero once the owner is stale.</summary>
    public Vec2 FeedForwardVelocity => IsOwnerStale ? Vec2.Zero : sampleVelocity;

    // A sign change of the correction along the heading; corrections inside the epsilon keep the last sign.
    private void CountOscillation(float along)
    {
        if (Math.Abs(along) < OscillationEpsilon) return;

        int sign = along > 0f ? 1 : -1;
        if (lastSign != 0 && sign != lastSign) Oscillations++;
        lastSign = sign;
    }

    /// <summary>0 below <paramref name="edge0"/>, 1 above <paramref name="edge1"/>, a C1-smooth ramp in between.</summary>
    internal static float SmoothStep(float edge0, float edge1, float value)
    {
        if (edge1 <= edge0) return value > edge0 ? 1f : 0f;

        float t = Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    /// <summary>An angle in radians wrapped into [-pi, pi].</summary>
    internal static float WrapAngle(float radians)
    {
        const float TwoPi = (float)(2 * Math.PI);
        radians %= TwoPi;
        if (radians > Math.PI) radians -= TwoPi;
        else if (radians < -Math.PI) radians += TwoPi;
        return radians;
    }

    /// <summary>The vector shortened to at most <paramref name="max"/>.</summary>
    internal static Vec2 ClampLength(Vec2 vector, float max)
    {
        float length = vector.Length;
        return length > max && length > 0f ? vector * (max / length) : vector;
    }

    // The value limited to [min, max].
    private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

    /// <summary>The yaw of a frame's forward axis in radians, counter-clockwise from +x.</summary>
    internal static float YawOf(Vec3 forward) => (float)Math.Atan2(forward.y, forward.x);
}
