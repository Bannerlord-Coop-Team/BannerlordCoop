using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Library;

namespace Missions.Missiles.Message;

public enum MissileOutcomeKind
{
    Ground,
    BounceSettled,
    Body,
    Shield,
}

/// <summary>The visible result of a shot, reported only by its controlling client.</summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkMissileOutcome : ICommand
{
    [ProtoMember(1)]
    public Guid WorldItemId { get; }
    [ProtoMember(2)]
    public MissileOutcomeKind Kind { get; }
    [ProtoMember(3)]
    public Vec3 Origin { get; }
    // Ground rotations use four quaternion floats; attachments retain all nine matrix floats exactly.
    [ProtoMember(4, IsPacked = true)]
    public float[] Rotation { get; }
    [ProtoMember(5)]
    public Guid? VictimId { get; }
    [ProtoMember(6, DataFormat = DataFormat.ZigZag)]
    public int BoneIndex { get; }
    [ProtoMember(7)]
    public bool IsAttachedFrameLocal { get; }
    [ProtoMember(8, DataFormat = DataFormat.ZigZag)]
    public int ShieldSlot { get; }
    [ProtoMember(9)]
    public float RemainingLifeTime { get; }

    public NetworkMissileOutcome(Guid worldItemId, MissileOutcomeKind kind, Vec3 origin, float[] rotation,
        Guid? victimId, int boneIndex, bool isAttachedFrameLocal, int shieldSlot, float remainingLifeTime)
    {
        WorldItemId = worldItemId;
        Kind = kind;
        Origin = origin;
        Rotation = rotation;
        VictimId = victimId;
        BoneIndex = boneIndex;
        IsAttachedFrameLocal = isAttachedFrameLocal;
        ShieldSlot = shieldSlot;
        RemainingLifeTime = remainingLifeTime;
    }
}
