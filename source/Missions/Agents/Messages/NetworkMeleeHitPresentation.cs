using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Missions.Agents.Messages;

/// <summary>Replays collision presentation that native simulation produced only on the attacking peer.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkMeleeHitPresentation : IEvent
{
    [ProtoMember(1)]
    public Guid VictimAgentId { get; }

    [ProtoMember(2)]
    public bool IsMount { get; }

    [ProtoMember(3)]
    public MeleeHitPresentationKind Kind { get; }

    [ProtoMember(4)]
    public int CollisionBoneIndex { get; }

    [ProtoMember(5)]
    public Vec3 CollisionPosition { get; }

    [ProtoMember(6)]
    public WeaponClass AttackerWeaponClass { get; }

    [ProtoMember(7)]
    public int PhysicsMaterialIndex { get; }

    [ProtoMember(8)]
    public float Strength { get; }

    public NetworkMeleeHitPresentation(
        Guid victimAgentId,
        bool isMount,
        MeleeHitPresentationKind kind,
        int collisionBoneIndex,
        Vec3 collisionPosition,
        WeaponClass attackerWeaponClass,
        int physicsMaterialIndex,
        float strength)
    {
        VictimAgentId = victimAgentId;
        IsMount = isMount;
        Kind = kind;
        CollisionBoneIndex = collisionBoneIndex;
        CollisionPosition = collisionPosition;
        AttackerWeaponClass = attackerWeaponClass;
        PhysicsMaterialIndex = physicsMaterialIndex;
        Strength = strength;
    }
}
