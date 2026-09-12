using Common.Messaging;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines.Messages;

/// <summary>
/// The server's BombardTick fired a siege bombardment missile from one side. Carries the campaign times
/// as ticks so Coop.Core never touches CampaignTime.
/// </summary>
public readonly struct SiegeEngineMissileAdded : IEvent
{
    public readonly SiegeEvent SiegeEvent;
    public readonly BattleSideEnum Side;
    public readonly SiegeEngineType ShooterType;
    public readonly int ShooterSlotIndex;
    public readonly SiegeBombardTargets TargetType;
    public readonly int TargetSlotIndex;
    public readonly SiegeEngineConstructionProgress TargetSiegeEngine;
    public readonly long CollisionTicks;
    public readonly long FireTicks;
    public readonly bool HitSuccessful;

    public SiegeEngineMissileAdded(SiegeEvent siegeEvent, BattleSideEnum side, SiegeEngineType shooterType, int shooterSlotIndex, SiegeBombardTargets targetType, int targetSlotIndex, SiegeEngineConstructionProgress targetSiegeEngine, long collisionTicks, long fireTicks, bool hitSuccessful)
    {
        SiegeEvent = siegeEvent;
        Side = side;
        ShooterType = shooterType;
        ShooterSlotIndex = shooterSlotIndex;
        TargetType = targetType;
        TargetSlotIndex = targetSlotIndex;
        TargetSiegeEngine = targetSiegeEngine;
        CollisionTicks = collisionTicks;
        FireTicks = fireTicks;
        HitSuccessful = hitSuccessful;
    }
}
