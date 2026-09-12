using Common.Messaging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Local (broker-only) event: a ranged siege weapon on the mission host just launched a projectile, carrying
/// the host's resolved launch. The Missions battle controller broadcasts it so peers play the fire animation
/// and throw a cosmetic replica projectile. Published by <see cref="Patches.SiegeWeaponFireCapturePatch"/>.
/// </summary>
public record SiegeWeaponFired : IEvent
{
    public RangedSiegeWeapon Weapon { get; }
    /// <summary>The pilot that fired; may be null if the machine had no shooter. Used to key the peer's replica.</summary>
    public Agent Shooter { get; }
    public Vec3 Position { get; }
    public Vec3 Direction { get; }
    public Mat3 Orientation { get; }
    public float BaseSpeed { get; }
    public float Speed { get; }
    public ItemObject MissileItem { get; }

    public SiegeWeaponFired(RangedSiegeWeapon weapon, Agent shooter, Vec3 position, Vec3 direction, Mat3 orientation, float baseSpeed, float speed, ItemObject missileItem)
    {
        Weapon = weapon;
        Shooter = shooter;
        Position = position;
        Direction = direction;
        Orientation = orientation;
        BaseSpeed = baseSpeed;
        Speed = speed;
        MissileItem = missileItem;
    }
}
