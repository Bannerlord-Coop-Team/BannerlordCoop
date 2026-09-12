using Common.Messaging;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// A replicated siege bombardment missile to reconstruct and add on the client so it renders the firing
/// animation. Ids and ticks only; the handler resolves the objects on the game thread.
/// </summary>
public readonly struct ApplySiegeEngineMissile : IEvent
{
    public readonly string SiegeEventId;
    public readonly int Side;
    public readonly string ShooterEngineTypeId;
    public readonly int ShooterSlotIndex;
    public readonly int TargetType;
    public readonly int TargetSlotIndex;
    public readonly string TargetSiegeEngineId;
    public readonly long CollisionTicks;
    public readonly long FireTicks;
    public readonly bool HitSuccessful;

    public ApplySiegeEngineMissile(string siegeEventId, int side, string shooterEngineTypeId, int shooterSlotIndex, int targetType, int targetSlotIndex, string targetSiegeEngineId, long collisionTicks, long fireTicks, bool hitSuccessful)
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
