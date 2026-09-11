#if DEBUG
using System;
using System.Linq;
using ProtoBuf;
using TaleWorlds.Library;

namespace Missions.Messages;

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabPresentation
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly NetworkNavalLabSailPresentation[] Sails;
    [ProtoMember(3)] public readonly NetworkNavalLabOarPresentation[] Oars;
    // Each side: phase, visual phase, phase rate, cycle arc, needed revolution rate.
    [ProtoMember(4)] public readonly float[] Sides;
    public NetworkNavalLabPresentation(Guid shipId, NetworkNavalLabSailPresentation[] sails,
        NetworkNavalLabOarPresentation[] oars, float[] sides)
    { ShipId = shipId; Sails = sails; Oars = oars; Sides = sides; }

    public bool IsValid => ShipId != Guid.Empty && Sails != null && Sails.Length > 0 && Sails.Length <= 16
        && Sails.All(sail => sail != null && sail.IsValid) && Sails.Select(sail => sail.Key).Distinct().Count() == Sails.Length
        && Oars != null && Oars.Length > 0 && Oars.Length <= 128 && Oars.All(oar => oar != null && oar.IsValid)
        && Oars.Select(oar => oar.Key).Distinct().Count() == Oars.Length
        && Sides != null && Sides.Length == 10 && Sides.All(value => Bounded(value, -100, 100));

    internal static bool Bounded(float value, float min, float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
    internal static bool ValidKey(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 256;
    public static float BlendPhase(float start, float target, float alpha)
    {
        const float turn = (float)(Math.PI * 2);
        float delta = (target - start) % turn;
        if (delta > Math.PI) delta -= turn;
        if (delta < -Math.PI) delta += turn;
        float result = (start + (delta * alpha)) % turn;
        if (result > Math.PI) result -= turn;
        if (result < -Math.PI) result += turn;
        return result;
    }

    public NetworkNavalLabPresentation BlendFrom(NetworkNavalLabPresentation start, float alpha)
    {
        if (alpha >= 1) return this;
        var sides = new float[10];
        for (int i = 0; i < sides.Length; i++)
            sides[i] = i % 5 < 2 ? BlendPhase(start.Sides[i], Sides[i], alpha) : start.Sides[i] + ((Sides[i] - start.Sides[i]) * alpha);
        return new NetworkNavalLabPresentation(ShipId,
            Sails.Select((sail, i) => sail.BlendFrom(start.Sails[i], alpha)).ToArray(),
            Oars.Select((oar, i) => oar.BlendFrom(start.Oars[i], alpha)).ToArray(), sides);
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabSailPresentation
{
    [ProtoMember(1)] public readonly string Key;
    [ProtoMember(2)] public readonly int Type;
    [ProtoMember(3)] public readonly float Target;
    [ProtoMember(4)] public readonly float Setting;
    [ProtoMember(5)] public readonly float Yaw;
    [ProtoMember(6)] public readonly bool Enabled;
    [ProtoMember(7)] public readonly bool Folding;
    [ProtoMember(8)] public readonly bool Unfolding;
    [ProtoMember(9)] public readonly float Progress;
    [ProtoMember(10)] public readonly float RealProgress;
    public NetworkNavalLabSailPresentation(string key, int type, float target, float setting, float yaw,
        bool enabled, bool folding, bool unfolding, float progress, float realProgress)
    { Key = key; Type = type; Target = target; Setting = setting; Yaw = yaw; Enabled = enabled;
        Folding = folding; Unfolding = unfolding; Progress = progress; RealProgress = realProgress; }
    public bool IsValid => NetworkNavalLabPresentation.ValidKey(Key) && Type >= 0 && Type <= 1
        && NetworkNavalLabPresentation.Bounded(Target, 0, 1) && NetworkNavalLabPresentation.Bounded(Setting, 0, 1)
        && NetworkNavalLabPresentation.Bounded(Yaw, -7, 7) && !(Folding && Unfolding)
        && NetworkNavalLabPresentation.Bounded(Progress, 0, 120) && NetworkNavalLabPresentation.Bounded(RealProgress, 0, 120);
    public NetworkNavalLabSailPresentation BlendFrom(NetworkNavalLabSailPresentation start, float alpha)
    {
        // Direction changes have different native progress coordinates; use the new authoritative baseline.
        bool sameDirection = Folding == start.Folding && Unfolding == start.Unfolding;
        return new NetworkNavalLabSailPresentation(Key, Type, Target, start.Setting + ((Setting - start.Setting) * alpha),
            NetworkNavalLabPresentation.BlendPhase(start.Yaw, Yaw, alpha), Enabled, Folding, Unfolding,
            sameDirection ? start.Progress + ((Progress - start.Progress) * alpha) : Progress,
            sameDirection ? start.RealProgress + ((RealProgress - start.RealProgress) * alpha) : RealProgress);
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNavalLabOarPresentation
{
    [ProtoMember(1)] public readonly string Key;
    [ProtoMember(2)] public readonly int Side;
    [ProtoMember(3)] public readonly float Phase;
    [ProtoMember(4)] public readonly float Extraction;
    [ProtoMember(5)] public readonly float NeededRate;
    [ProtoMember(6)] public readonly bool Rowing;
    [ProtoMember(7)] public readonly float[] BladeFrame;
    public NetworkNavalLabOarPresentation(string key, int side, float phase, float extraction, float neededRate, bool rowing, float[] bladeFrame)
    { Key = key; Side = side; Phase = phase; Extraction = extraction; NeededRate = neededRate; Rowing = rowing; BladeFrame = bladeFrame; }
    public bool IsValid => NetworkNavalLabPresentation.ValidKey(Key) && Side >= 0 && Side <= 1
        && NetworkNavalLabPresentation.Bounded(Phase, -7, 7) && NetworkNavalLabPresentation.Bounded(Extraction, 0, 1)
        && NetworkNavalLabPresentation.Bounded(NeededRate, -100, 100) && ValidFrame(BladeFrame);
    public static bool ValidFrame(float[] frame)
    {
        if (frame == null || frame.Length != 12 || frame.Any(value => !NetworkNavalLabPresentation.Bounded(value, -1000, 1000))) return false;
        var rotation = ToFrame(frame).rotation;
        return Math.Abs(rotation.s.LengthSquared - 1) < 0.02f && Math.Abs(rotation.f.LengthSquared - 1) < 0.02f
            && Math.Abs(rotation.u.LengthSquared - 1) < 0.02f && Math.Abs(Vec3.DotProduct(rotation.s, rotation.f)) < 0.02f
            && Vec3.DotProduct(Vec3.CrossProduct(rotation.s, rotation.f), rotation.u) > 0.98f;
    }
    public static float[] FromFrame(MatrixFrame frame) => new[] { frame.rotation.s.x, frame.rotation.s.y, frame.rotation.s.z,
        frame.rotation.f.x, frame.rotation.f.y, frame.rotation.f.z, frame.rotation.u.x, frame.rotation.u.y, frame.rotation.u.z,
        frame.origin.x, frame.origin.y, frame.origin.z };
    public static MatrixFrame ToFrame(float[] values) => new(new Mat3(new Vec3(values[0], values[1], values[2]),
        new Vec3(values[3], values[4], values[5]), new Vec3(values[6], values[7], values[8])), new Vec3(values[9], values[10], values[11]));
    public NetworkNavalLabOarPresentation BlendFrom(NetworkNavalLabOarPresentation start, float alpha) => new(Key, Side,
        NetworkNavalLabPresentation.BlendPhase(start.Phase, Phase, alpha), start.Extraction + ((Extraction - start.Extraction) * alpha),
        NeededRate, Rowing, FromFrame(MatrixFrame.Lerp(ToFrame(start.BladeFrame), ToFrame(BladeFrame), alpha)));
}
#endif
