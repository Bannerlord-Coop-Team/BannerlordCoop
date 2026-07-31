using Common.Messaging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Messages;

public enum MeleeHitPresentationKind
{
    Blood = 1,
    ShieldImpact = 2
}

/// <summary>Local collision result selected by the attacking peer.</summary>
public readonly struct MeleeHitPresentation : IEvent
{
    public Agent Victim { get; }
    public MeleeHitPresentationKind Kind { get; }
    public sbyte CollisionBoneIndex { get; }
    public Vec3 CollisionPosition { get; }
    public WeaponClass AttackerWeaponClass { get; }
    public int PhysicsMaterialIndex { get; }
    public float Strength { get; }

    public MeleeHitPresentation(
        Agent victim,
        MeleeHitPresentationKind kind,
        sbyte collisionBoneIndex,
        Vec3 collisionPosition,
        WeaponClass attackerWeaponClass,
        int physicsMaterialIndex,
        float strength)
    {
        Victim = victim;
        Kind = kind;
        CollisionBoneIndex = collisionBoneIndex;
        CollisionPosition = collisionPosition;
        AttackerWeaponClass = attackerWeaponClass;
        PhysicsMaterialIndex = physicsMaterialIndex;
        Strength = strength;
    }
}
