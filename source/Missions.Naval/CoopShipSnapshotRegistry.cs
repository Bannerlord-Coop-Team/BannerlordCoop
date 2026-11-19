using GameInterface;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Naval;

namespace Missions.Naval;

/// <summary>
/// The detached mission ships <see cref="CoopShipSnapshotBuilder"/> built on this client, with the campaign ship each one stands
/// for. The campaign gates resolve it.
/// </summary>
public interface ICoopShipSnapshotRegistry
{
    /// <summary>Records a new snapshot of the campaign ship with registry id <paramref name="campaignShipId"/>.</summary>
    void Add(Ship ship, string campaignShipId);

    /// <summary>True for a ship the builder made, which the campaign gates keep off the campaign.</summary>
    bool Contains(Ship ship);

    /// <summary>The registry id of the campaign ship a snapshot stands for; null for any other ship or an unregistered source.</summary>
    string CampaignShipIdOf(Ship ship);
}

/// <summary>One per client container; the keys are weak, so a snapshot leaves with its mission.</summary>
internal class CoopShipSnapshotRegistry : ICoopShipSnapshotRegistry
{
    private readonly ConditionalWeakTable<Ship, string> snapshots = new ConditionalWeakTable<Ship, string>();

    /// <inheritdoc/>
    public void Add(Ship ship, string campaignShipId) => snapshots.Add(ship, campaignShipId);

    /// <inheritdoc/>
    public bool Contains(Ship ship) => ship != null && snapshots.TryGetValue(ship, out _);

    /// <inheritdoc/>
    public string CampaignShipIdOf(Ship ship) =>
        ship != null && snapshots.TryGetValue(ship, out var campaignShipId) ? campaignShipId : null;

    /// <summary>[Any thread] Whether the ship is a snapshot in the client container patches resolve from; false when none resolves.</summary>
    internal static bool IsSnapshotHere(Ship ship) =>
        ContainerProvider.TryResolve<ICoopShipSnapshotRegistry>(out var snapshots) && snapshots.Contains(ship);
}
