using Common.Messaging;
using ProtoBuf;
using System;
using System.Linq;
using TaleWorlds.Library;

namespace Missions.Messages;

/// <summary>Owner to peers over the mission mesh, 20 Hz: one owned hull's world frame, helm input and ropes.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkBattleShipSample : IEvent
{
    public const int FrameLength = 12;

    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string OwnerControllerId;
    [ProtoMember(3)] public readonly long Sequence;
    [ProtoMember(5)] public readonly float[] Frame;
    /// <summary>The hull's helm input, replayed on copies for oar and sail presentation.</summary>
    [ProtoMember(6)] public readonly BattleShipInput Input;
    /// <summary>The hull's rope at every throw station that has thrown one, null when none has.</summary>
    [ProtoMember(7)] public readonly BattleRopeState[] Ropes;
    /// <summary>The sender's battle host epoch; AI-hull samples from a superseded host generation are dropped.</summary>
    [ProtoMember(8)] public readonly int HostEpoch;

    public NetworkBattleShipSample(Guid shipId, string ownerControllerId, long sequence, float[] frame,
        BattleShipInput input = default, BattleRopeState[] ropes = null, int hostEpoch = 0)
    {
        Ropes = ropes;
        HostEpoch = hostEpoch;
        ShipId = shipId;
        OwnerControllerId = ownerControllerId;
        Sequence = sequence;
        Frame = frame;
        Input = input;
    }

    public bool HasValidFrame => IsValidFrame(Frame);

    public static float[] FromFrame(MatrixFrame frame) => new[]
    {
        frame.rotation.s.x, frame.rotation.s.y, frame.rotation.s.z,
        frame.rotation.f.x, frame.rotation.f.y, frame.rotation.f.z,
        frame.rotation.u.x, frame.rotation.u.y, frame.rotation.u.z,
        frame.origin.x, frame.origin.y, frame.origin.z,
    };

    public static MatrixFrame ToFrame(float[] values) => new MatrixFrame(
        new Mat3(new Vec3(values[0], values[1], values[2]), new Vec3(values[3], values[4], values[5]),
            new Vec3(values[6], values[7], values[8])),
        new Vec3(values[9], values[10], values[11]));

    // Finite, scene-bounded and an orthonormal right-handed rotation.
    public static bool IsValidFrame(float[] frame)
    {
        if (frame == null || frame.Length != FrameLength ||
            frame.Any(value => float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 10000f))
            return false;

        var rotation = ToFrame(frame).rotation;
        return Math.Abs(rotation.s.LengthSquared - 1) < 0.02f && Math.Abs(rotation.f.LengthSquared - 1) < 0.02f
            && Math.Abs(rotation.u.LengthSquared - 1) < 0.02f && Math.Abs(Vec3.DotProduct(rotation.s, rotation.f)) < 0.02f
            && Vec3.DotProduct(Vec3.CrossProduct(rotation.s, rotation.f), rotation.u) > 0.98f;
    }
}
