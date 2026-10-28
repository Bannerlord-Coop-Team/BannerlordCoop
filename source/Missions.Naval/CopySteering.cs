using System;
using System.Globalization;
using TaleWorlds.Library;

namespace Missions.Naval;

/// <summary>The steered copies' correction knobs; production defaults, which DEBUG <c>coop.debug.naval.copy_steer</c> tunes live.</summary>
public sealed class CopySteeringKnobs
{
    internal const string KnobNames = "gain window softzone clamp tau lead snap roll";

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

    /// <summary>
    /// Seconds the target is extrapolated past the sample's age, for a transit delay the age misses; 0 because on a LAN it is
    /// about 0 and any lead puts the copy that far ahead of its owner.
    /// </summary>
    public float LeadSeconds { get; private set; }

    /// <summary>Planar error or sample jump in metres beyond which the copy is teleported to the owner's frame.</summary>
    public float SnapMetres { get; private set; } = 25f;

    /// <summary>
    /// Share of a roped copy's roll error closed per second on top of its owner's roll rate; 0 leaves the roll to local
    /// buoyancy. With the 0.2 s tau blend, 5 damps the error at about half of critical, so it settles without ringing.
    /// </summary>
    public float RollGainPerSecond { get; private set; } = 5f;

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
            case "roll": return Set(value, 0f, 20f, "roll (1/s)", v => RollGainPerSecond = v);
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

    /// <summary>Every knob as key=value, as the copy_steer command echoes them.</summary>
    public override string ToString() => string.Format(CultureInfo.InvariantCulture,
        "gain={0} window={1} softzone={2} clamp={3} tau={4} lead={5} snap={6} roll={7}",
        Gain, WindowSeconds, SoftZoneMetres, ClampMetresPerSecond, TauSeconds, LeadSeconds, SnapMetres, RollGainPerSecond);
}

/// <summary>One correction step for a steered copy: the velocity change to apply, or a snap to the owner's frame.</summary>
internal readonly struct CopySteeringStep
{
    /// <summary>A step that snaps, or changes the body's velocity, yaw rate and roll rate; the along-heading error is for diagnostics.</summary>
    public CopySteeringStep(bool snap, Vec2 deltaVelocity, float deltaYawRate, float errorAlongHeading, float deltaRollRate = 0f)
    {
        Snap = snap;
        DeltaVelocity = deltaVelocity;
        DeltaYawRate = deltaYawRate;
        ErrorAlongHeading = errorAlongHeading;
        DeltaRollRate = deltaRollRate;
    }

    /// <summary>The copy fell too far behind: a snap is queued (<see cref="CopySteering.IsSnapQueued"/>) instead of steering.</summary>
    public bool Snap { get; }

    /// <summary>Planar velocity change in m/s.</summary>
    public Vec2 DeltaVelocity { get; }

    /// <summary>Yaw rate change in rad/s.</summary>
    public float DeltaYawRate { get; }

    /// <summary>Roll rate change in rad/s about the hull's forward axis, right-handed; 0 when the roll isn't steered.</summary>
    public float DeltaRollRate { get; }

    /// <summary>The position error to the target along the copy's heading in metres.</summary>
    public float ErrorAlongHeading { get; }
}

/// <summary>
/// Steers a copy's planar position, yaw and, when roped, roll toward its owner's extrapolated sample: the owner's rate plus a
/// clamped correction, blended in over tau. Heave and pitch float on local water (NavalBattles.md section 1).
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
    // The largest roll correction on top of the owner's roll rate, so a rope's sudden heel is followed over a few tenths of a second.
    internal const float RollClampDegreesPerSecond = 10f;
    internal const float DegToRad = (float)(Math.PI / 180.0);

    private readonly CopySteeringKnobs knobs;
    private MatrixFrame sampleFrame;
    private Vec2 sampleVelocity;
    private float sampleYawRate;
    private float sampleRoll;
    private float sampleRollRate;

    /// <summary>A steering that reads the shared knobs on every step, so a tuned knob reaches it at once.</summary>
    public CopySteering(CopySteeringKnobs knobs)
    {
        this.knobs = knobs;
    }

    /// <summary>Whether an owner sample arrived, without which there is nothing to steer toward.</summary>
    public bool HasTarget { get; private set; }

    /// <summary>
    /// A snap waits for the mission tick: a sample jumped past the snap distance or the copy fell that far behind. The
    /// physics steps leave the copy alone until <see cref="TakeSnapFrame"/> clears it.
    /// </summary>
    public bool IsSnapQueued { get; private set; }

    /// <summary>The owner's latest sample frame as received, not extrapolated.</summary>
    public MatrixFrame SampleFrame => sampleFrame;

    /// <summary>Where the owner is estimated to be right now: the sample moved on by its velocity for its age, without the lead.</summary>
    internal (Vec2 Position, float Yaw) OwnerEstimate => Extrapolate(MaxExtrapolationSeconds, 0f);

    /// <summary>Physics time stepped since the latest sample arrived.</summary>
    public float SecondsSinceSample { get; private set; }

    /// <summary>The owner's samples stopped coming, so their velocity no longer feeds forward.</summary>
    public bool IsOwnerStale => SecondsSinceSample > StaleSeconds;

    /// <summary>Takes the owner's latest sample; a jump past the snap distance from where the last one led queues a snap.</summary>
    public void ObserveOwner(MatrixFrame frame, Vec2 velocity, float yawRate)
    {
        var led = Extrapolate(StaleSeconds, knobs.LeadSeconds).Position;
        if (HasTarget && (frame.origin.AsVec2 - led).Length > knobs.SnapMetres) IsSnapQueued = true;

        float roll = RollOf(frame.rotation);
        sampleRollRate = OwnerRollRate(roll);
        sampleRoll = roll;
        sampleFrame = frame;
        sampleVelocity = velocity;
        sampleYawRate = yawRate;
        SecondsSinceSample = 0f;
        HasTarget = true;
    }

    // The owner's roll rate from its previous sample over the physics time stepped since, as samples carry none: a sample
    // with no step since the last keeps the last rate, and the first sample or one after a stale gap has none.
    private float OwnerRollRate(float roll)
    {
        if (!HasTarget || IsOwnerStale) return 0f;
        if (!(SecondsSinceSample > 0f)) return sampleRollRate;

        return WrapAngle(roll - sampleRoll) / SecondsSinceSample;
    }

    /// <summary>The planar position and yaw the copy steers toward right now.</summary>
    internal (Vec2 Position, float Yaw) Target => Extrapolate(MaxExtrapolationSeconds, knobs.LeadSeconds);

    /// <summary>The roll in radians a roped copy steers toward right now: the owner's sampled roll moved on by its roll rate.</summary>
    internal float TargetRoll => sampleRoll + (sampleRollRate * Ahead(MaxExtrapolationSeconds, knobs.LeadSeconds));

    /// <summary>
    /// The correction for one physics step of <paramref name="dt"/> seconds from the copy's current state; <paramref name="roll"/>
    /// is the copy's roll and roll rate when its roll is steered (a roped hull), null to leave the roll to local buoyancy.
    /// </summary>
    public CopySteeringStep Compute(float dt, Vec3 origin, float yaw, Vec2 velocity, float yawRate, (float Angle, float Rate)? roll = null)
    {
        if (!HasTarget || !(dt > 0f) || float.IsInfinity(dt)) return default;

        SecondsSinceSample += dt;
        var target = Target;
        var error = target.Position - origin.AsVec2;
        float distance = error.Length;
        if (IsSnapQueued || distance > knobs.SnapMetres)
        {
            IsSnapQueued = true;
            return new CopySteeringStep(true, Vec2.Zero, 0f, 0f);
        }

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

        float along = (error.x * (float)Math.Cos(yaw)) + (error.y * (float)Math.Sin(yaw));
        float deltaRollRate = roll.HasValue ? RollRateChange(roll.Value, stale, blend) : 0f;
        return new CopySteeringStep(false, deltaVelocity, deltaYawRate, along, deltaRollRate);
    }

    // The roll rate change toward the owner's roll rate plus a clamped pull to the target roll. Unlike the yaw it has no soft
    // zone: the pass limit is a 0.5 degree mean, and a linear pull is already 0 at no error.
    private float RollRateChange((float Angle, float Rate) roll, bool stale, float blend)
    {
        float gain = knobs.RollGainPerSecond;
        if (gain <= 0f) return 0f;

        float clamp = RollClampDegreesPerSecond * DegToRad;
        float correction = Clamp(WrapAngle(TargetRoll - roll.Angle) * gain, -clamp, clamp);
        return ((stale ? 0f : sampleRollRate) + correction - roll.Rate) * blend;
    }

    // The sample moved on by its velocity for its age, capped at maxAge, plus the lead.
    private (Vec2 Position, float Yaw) Extrapolate(float maxAge, float lead)
    {
        float ahead = Ahead(maxAge, lead);
        return (sampleFrame.origin.AsVec2 + (sampleVelocity * ahead), YawOf(sampleFrame.rotation.f) + (sampleYawRate * ahead));
    }

    // Seconds a sample is moved on: its age capped at maxAge, plus the lead.
    private float Ahead(float maxAge, float lead) => Math.Min(SecondsSinceSample, maxAge) + lead;

    /// <summary>Clears the queued snap and returns the frame it teleports to now, from the latest sample, which covers any jump since.</summary>
    public MatrixFrame TakeSnapFrame()
    {
        IsSnapQueued = false;
        return FrameAt(Target);
    }

    // The owner's sample frame moved and turned to the target.
    private MatrixFrame FrameAt((Vec2 Position, float Yaw) target)
    {
        var frame = sampleFrame;
        frame.origin = new Vec3(target.Position.x, target.Position.y, frame.origin.z);
        frame.rotation = RotateYaw(frame.rotation, WrapAngle(target.Yaw - YawOf(frame.rotation.f)));
        return frame;
    }

    /// <summary>The owner's velocity the snap carries over, zero once the owner is stale.</summary>
    public Vec2 FeedForwardVelocity => IsOwnerStale ? Vec2.Zero : sampleVelocity;

    /// <summary>The owner's yaw rate the snap carries over, zero once the owner is stale.</summary>
    public float FeedForwardYawRate => IsOwnerStale ? 0f : sampleYawRate;

    /// <summary>The rotation turned about the world up axis by <paramref name="radians"/>, counter-clockwise from above.</summary>
    internal static Mat3 RotateYaw(Mat3 rotation, float radians)
    {
        float sin = (float)Math.Sin(radians);
        float cos = (float)Math.Cos(radians);
        rotation.s = TurnAboutZ(rotation.s, sin, cos);
        rotation.f = TurnAboutZ(rotation.f, sin, cos);
        rotation.u = TurnAboutZ(rotation.u, sin, cos);
        return rotation;
    }

    // The vector turned about the world z axis by the angle whose sine and cosine are given.
    private static Vec3 TurnAboutZ(Vec3 vector, float sin, float cos) =>
        new Vec3((vector.x * cos) - (vector.y * sin), (vector.x * sin) + (vector.y * cos), vector.z);

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

    /// <summary>
    /// The roll in radians, right-handed about the forward axis as the angular velocity's forward component turns it; the
    /// roll probe's side-axis tilt has the opposite sign.
    /// </summary>
    internal static float RollOf(Mat3 rotation) => (float)Math.Atan2(-rotation.s.z, rotation.u.z);
}
