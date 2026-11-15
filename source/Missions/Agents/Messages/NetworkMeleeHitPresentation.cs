using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Missions.Agents.Messages;

/// <summary>Replays collision presentation selected by the peer processing the hit.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkMeleeHitPresentation : IEvent
{
    [ProtoMember(1)]
    public Guid VictimAgentId { get; }

    [ProtoMember(2)]
    public bool IsMount { get; }

    [ProtoMember(3)]
    public MeleeHitPresentationKind Kind { get; }

    // The index fields below are shifted by one so protobuf omits their usual -1 instead of writing a 10-byte varint.
    [ProtoMember(4)]
    private int CollisionBoneIndexWire { get; }
    public int CollisionBoneIndex => CollisionBoneIndexWire - 1;

    [ProtoMember(5)]
    public Vec3 CollisionPosition { get; }

    [ProtoMember(6)]
    public WeaponClass AttackerWeaponClass { get; }

    [ProtoMember(7)]
    private int PhysicsMaterialIndexWire { get; }
    public int PhysicsMaterialIndex => PhysicsMaterialIndexWire - 1;

    [ProtoMember(8)]
    public float Strength { get; }

    [ProtoMember(9)]
    private int SoundIndexWire { get; }
    public int SoundIndex => SoundIndexWire - 1;

    [ProtoMember(10)]
    public float ArmorType { get; }

    public NetworkMeleeHitPresentation(
        Guid victimAgentId,
        bool isMount,
        MeleeHitPresentationKind kind,
        int collisionBoneIndex,
        Vec3 collisionPosition,
        WeaponClass attackerWeaponClass,
        int physicsMaterialIndex,
        float strength,
        int soundIndex = -1,
        float armorType = 0f)
    {
        VictimAgentId = victimAgentId;
        IsMount = isMount;
        Kind = kind;
        CollisionBoneIndexWire = collisionBoneIndex + 1;
        CollisionPosition = collisionPosition;
        AttackerWeaponClass = attackerWeaponClass;
        PhysicsMaterialIndexWire = physicsMaterialIndex + 1;
        Strength = strength;
        SoundIndexWire = soundIndex + 1;
        ArmorType = armorType;
    }
}
