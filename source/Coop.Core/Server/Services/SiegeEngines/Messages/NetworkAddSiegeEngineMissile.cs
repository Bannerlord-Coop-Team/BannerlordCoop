using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEngines.Messages;

/// <summary>
/// Notify clients of a siege bombardment missile the server fired, so they render the firing animation.
/// Engine damage is already replicated through the engine hitpoints; this carries only the visual projectile.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkAddSiegeEngineMissile : IEvent
{
    [ProtoMember(1)]
    public string SiegeEventId { get; }
    [ProtoMember(2)]
    public int Side { get; }
    [ProtoMember(3)]
    public string ShooterEngineTypeId { get; }
    [ProtoMember(4)]
    public int ShooterSlotIndex { get; }
    [ProtoMember(5)]
    public int TargetType { get; }
    [ProtoMember(6)]
    public int TargetSlotIndex { get; }
    [ProtoMember(7)]
    public string TargetSiegeEngineId { get; }
    [ProtoMember(8)]
    public long CollisionTicks { get; }
    [ProtoMember(9)]
    public long FireTicks { get; }
    [ProtoMember(10)]
    public bool HitSuccessful { get; }

    public NetworkAddSiegeEngineMissile(string siegeEventId, int side, string shooterEngineTypeId, int shooterSlotIndex, int targetType, int targetSlotIndex, string targetSiegeEngineId, long collisionTicks, long fireTicks, bool hitSuccessful)
    {
        SiegeEventId = siegeEventId;
        Side = side;
        ShooterEngineTypeId = shooterEngineTypeId;
        ShooterSlotIndex = shooterSlotIndex;
        TargetType = targetType;
        TargetSlotIndex = targetSlotIndex;
        TargetSiegeEngineId = targetSiegeEngineId;
        CollisionTicks = collisionTicks;
        FireTicks = fireTicks;
        HitSuccessful = hitSuccessful;
    }
}
