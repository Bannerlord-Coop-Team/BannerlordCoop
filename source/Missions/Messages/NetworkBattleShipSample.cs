using Common.Messaging;
using Missions.Battles;
using ProtoBuf;
using System;
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
    /// <summary>The owner's planar body velocity in m/s, which copies extrapolate the frame with.</summary>
    [ProtoMember(9)] public readonly float VelocityX;
    [ProtoMember(10)] public readonly float VelocityY;
    /// <summary>The owner's body yaw rate in rad/s.</summary>
    [ProtoMember(11)] public readonly float YawRate;
    /// <summary>The owner's committed crew mass in kg, read only with <see cref="HasCrewLoad"/>.</summary>
    [ProtoMember(12)] public readonly float CrewMass;
    /// <summary>The crew's centre of mass in the owner's hull body frame.</summary>
    [ProtoMember(13)] public readonly float CrewLocalX;
    [ProtoMember(14)] public readonly float CrewLocalY;
    [ProtoMember(15)] public readonly float CrewLocalZ;
    /// <summary>Whether the sample carries a crew load at all; a zero <see cref="CrewMass"/> then means a known-empty deck.</summary>
    [ProtoMember(16)] public readonly bool HasCrewLoad;
    /// <summary>The owner's body roll rate in rad/s about its forward axis, so a copy needn't differentiate unevenly arriving frames.</summary>
    [ProtoMember(17)] public readonly float RollRate;

    /// <summary>One sample; the motion is flattened into the velocity, yaw rate and crew load members.</summary>
    public NetworkBattleShipSample(Guid shipId, string ownerControllerId, long sequence, float[] frame,
        BattleShipInput input = default, BattleRopeState[] ropes = null, int hostEpoch = 0, BattleShipMotion motion = default)
    {
        Ropes = ropes;
        HostEpoch = hostEpoch;
        VelocityX = motion.Velocity.x;
        VelocityY = motion.Velocity.y;
        YawRate = motion.YawRate;
        RollRate = motion.RollRate;
        ShipId = shipId;
        OwnerControllerId = ownerControllerId;
        Sequence = sequence;
        Frame = frame;
        Input = input;
        if (motion.CrewLoad == null) return;

        HasCrewLoad = true;
        CrewMass = motion.CrewLoad.Mass;
        CrewLocalX = motion.CrewLoad.LocalCentre.x;
        CrewLocalY = motion.CrewLoad.LocalCentre.y;
        CrewLocalZ = motion.CrewLoad.LocalCentre.z;
    }

    public AuthorityStamp Stamp => new AuthorityStamp(OwnerControllerId, HostEpoch, Sequence);

    public bool HasValidFrame => IsValidFrame(Frame);

    /// <summary>The sampled motion rebuilt from the wire members; a crew load that isn't finite or is negative arrives as unknown.</summary>
    public BattleShipMotion Motion => new BattleShipMotion(new Vec2(VelocityX, VelocityY), YawRate,
        HasCrewLoad ? BattleShipCrewLoad.Create(CrewMass, new Vec3(CrewLocalX, CrewLocalY, CrewLocalZ)) : null, RollRate);

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

    public static bool IsValidFrame(float[] frame) =>
        frame != null && frame.Length == FrameLength && FrameValidation.IsValid(ToFrame(frame));
}
