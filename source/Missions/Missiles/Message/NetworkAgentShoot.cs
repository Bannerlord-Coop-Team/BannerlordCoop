using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Missions.Missiles.Message;

/// <summary>Resolved launch data for a missile fired by a registered agent.</summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkAgentShoot : ICommand
{
    [ProtoMember(1)]
    public Guid AgentId { get; }
    [ProtoMember(2)]
    public Vec3 Position { get; }
    [ProtoMember(3)]
    public Vec3 Velocity { get; }
    // Only set when the frame doesn't fit PackedOrientation within MissileOrientationCodec.Tolerance.
    [ProtoMember(4)]
    private Mat3? ExactOrientation { get; }
    [ProtoMember(5)]
    public bool HasRigidBody { get; }
    [ProtoMember(6)]
    public string MissileItemId { get; }
    [ProtoMember(7)]
    public string ItemModifierId { get; }
    [ProtoMember(8)]
    public Banner Banner { get; }
    [ProtoMember(10)]
    public float BaseSpeed { get; }
    [ProtoMember(11)]
    public float Speed { get; }
    [ProtoMember(12)]
    public int CurrentUsageIndex { get; }
    [ProtoMember(13)]
    public long ShotSequence { get; }
    [ProtoMember(14, DataFormat = DataFormat.FixedSize)]
    private ulong PackedOrientation { get; }

    public Mat3 Orientation => ExactOrientation ?? MissileOrientationCodec.Unpack(PackedOrientation);

    public NetworkAgentShoot(Guid agentId, Vec3 position, Vec3 velocity, Mat3 orientation,
        bool hasRigidBody, string missileItemId, string itemModifierId, Banner banner,
        float baseSpeed, float speed, int currentUsageIndex, long shotSequence)
    {
        AgentId = agentId;
        Position = position;
        Velocity = velocity;
        if (MissileOrientationCodec.TryPack(orientation, out ulong packed))
            PackedOrientation = packed;
        else
            ExactOrientation = orientation;
        HasRigidBody = hasRigidBody;
        MissileItemId = missileItemId;
        ItemModifierId = itemModifierId;
        Banner = banner;
        BaseSpeed = baseSpeed;
        Speed = speed;
        CurrentUsageIndex = currentUsageIndex;
        ShotSequence = shotSequence;
    }
}
