using Common.Messaging;

namespace GameInterface.Services.SiegeEnginesConstructionProgress.Messages;

/// <summary>
/// Has GameInterface apply a siege engine's hitpoints and max hitpoints on this client.
/// </summary>
public record ChangeSiegeEngineHitpoints : ICommand
{
    public string SiegeEngineId { get; }
    public float Hitpoints { get; }
    public float MaxHitPoints { get; }

    public ChangeSiegeEngineHitpoints(string siegeEngineId, float hitpoints, float maxHitPoints)
    {
        SiegeEngineId = siegeEngineId;
        Hitpoints = hitpoints;
        MaxHitPoints = maxHitPoints;
    }
}
