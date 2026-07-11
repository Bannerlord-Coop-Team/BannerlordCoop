using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Local (broker-only) event raised when a deployment point deploys or disbands a siege engine in a
/// coop siege mission. The Missions battle controller broadcasts it over the mesh when this client is
/// the engine deployer, so every mission ends up with the same physical engine layout.
/// </summary>
public record SiegeEnginePlacementChanged : IEvent
{
    public DeploymentPoint Point { get; }

    /// <summary>Weapon type name deployed at the point, or null for a disband.</summary>
    public string WeaponTypeName { get; }

    public SiegeEnginePlacementChanged(DeploymentPoint point, string weaponTypeName)
    {
        Point = point;
        WeaponTypeName = weaponTypeName;
    }
}
