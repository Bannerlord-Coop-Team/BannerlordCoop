using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;
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
    // The shooter's shield slot, so a victim who switched shields since keeps the missile on the one that was hit.
    [ProtoMember(7)]
    public EquipmentIndex ShieldSlot { get; }
    [ProtoMember(9)]
    public float RemainingLifeTime { get; }
    [ProtoMember(10)]
    public int? ParentMissionObjectId { get; }
    [ProtoMember(11)]
    public Guid? ParentWorldItemId { get; }
    [ProtoMember(12)]
    public Guid? ParentShipId { get; }
    [ProtoMember(13)]
    public string ParentShipObjectKey { get; }
    [ProtoMember(14)]
    public int? ParentShipObjectIndex { get; }
    [ProtoMember(15)]
    public string ParentEntityPath { get; }

    public NetworkMissileOutcome(Guid worldItemId, MissileOutcomeKind kind, Vec3 origin, float[] rotation,
        Guid? victimId, int boneIndex, float remainingLifeTime,
        int? parentMissionObjectId = null, Guid? parentWorldItemId = null, Guid? parentShipId = null,
        string parentShipObjectKey = null, int? parentShipObjectIndex = null, string parentEntityPath = null,
        EquipmentIndex shieldSlot = EquipmentIndex.WeaponItemBeginSlot)
    {
        WorldItemId = worldItemId;
        Kind = kind;
        Origin = origin;
        Rotation = rotation;
        VictimId = victimId;
        BoneIndex = boneIndex;
        RemainingLifeTime = remainingLifeTime;
        ParentMissionObjectId = parentMissionObjectId;
        ParentWorldItemId = parentWorldItemId;
        ParentShipId = parentShipId;
        ParentShipObjectKey = parentShipObjectKey;
        ParentShipObjectIndex = parentShipObjectIndex;
        ParentEntityPath = parentEntityPath;
        ShieldSlot = shieldSlot;
    }
}
