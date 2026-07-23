using Common.Messaging;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesConstructionProgress.Messages;

/// <summary>
/// A siege engine's hitpoints changed on the server (battle damage), or it was just registered and its current
/// hitpoints need replicating. Carries MaxHitPoints too, because the client shell is created without it and the
/// constructor's initial values are set before the engine has a network id, so they never broadcast on their own.
/// </summary>
public readonly struct SiegeEngineHitpointsChanged : IEvent
{
    public readonly SiegeEngineConstructionProgress SiegeEngine;
    public readonly float Hitpoints;
    public readonly float MaxHitPoints;

    public SiegeEngineHitpointsChanged(SiegeEngineConstructionProgress siegeEngine, float hitpoints, float maxHitPoints)
    {
        SiegeEngine = siegeEngine;
        Hitpoints = hitpoints;
        MaxHitPoints = maxHitPoints;
    }
}
