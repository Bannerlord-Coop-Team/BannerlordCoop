using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// A ship siege weapon's state as its hull's owner simulates it: the aim targets vanilla turns the weapon towards, the
/// <c>RangedSiegeWeapon.WeaponState</c> and the ammo left.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class BattleShipWeaponState
{
    // RangedSiegeWeapon.WeaponState.Idle through ReloadingPaused.
    public const int MaxWeaponState = 8;
    private const float AimTolerance = 0.002f;

    public BattleShipWeaponState(float direction, float releaseAngle, int weaponState, int ammoCount)
    {
        Direction = direction;
        ReleaseAngle = releaseAngle;
        WeaponState = weaponState;
        AmmoCount = ammoCount;
    }

    [ProtoMember(1)] public float Direction { get; private set; }
    [ProtoMember(2)] public float ReleaseAngle { get; private set; }
    [ProtoMember(3)] public int WeaponState { get; private set; }
    [ProtoMember(4)] public int AmmoCount { get; private set; }

    public bool IsValid => IsFinite(Direction) && IsFinite(ReleaseAngle) && WeaponState >= 0 && WeaponState <= MaxWeaponState
        && AmmoCount >= 0;

    /// <summary>Whether peers holding <paramref name="previous"/> must be told about this state.</summary>
    public bool DiffersFrom(BattleShipWeaponState previous) =>
        previous == null || previous.WeaponState != WeaponState || previous.AmmoCount != AmmoCount
        || Math.Abs(previous.Direction - Direction) > AimTolerance || Math.Abs(previous.ReleaseAngle - ReleaseAngle) > AimTolerance;

    // Whether a float off the wire is a real number.
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
