using GameInterface;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Naval;

namespace Missions.Naval;

/// <summary>
/// The detached mission ships <see cref="CoopShipSnapshotBuilder"/> built on this client, with the campaign ship each one stands
/// for and the damage its upgrades mitigated. The campaign gates resolve it.
/// </summary>
public interface ICoopShipSnapshotRegistry
{
    /// <summary>Records a new snapshot of the campaign ship with registry id <paramref name="campaignShipId"/>.</summary>
    void Add(Ship ship, string campaignShipId);

    /// <summary>True for a ship the builder made, which the campaign gates keep off the campaign.</summary>
    bool Contains(Ship ship);

    /// <summary>The registry id of the campaign ship a snapshot stands for; null for any other ship or an unregistered source.</summary>
    string CampaignShipIdOf(Ship ship);

    /// <summary>Adds damage the snapshot's upgrades mitigated on a hit, which vanilla would have turned into Boatswain XP.</summary>
    void AddMitigatedDamage(Ship ship, float mitigatedDamage);

    /// <summary>The damage the snapshot's upgrades mitigated so far in this mission; 0 for any other ship.</summary>
    float MitigatedDamageOf(Ship ship);
}

/// <summary>One per client container; the keys are weak, so a snapshot leaves with its mission.</summary>
internal class CoopShipSnapshotRegistry : ICoopShipSnapshotRegistry
{
    private readonly ConditionalWeakTable<Ship, Entry> snapshots = new ConditionalWeakTable<Ship, Entry>();

    /// <inheritdoc/>
    public void Add(Ship ship, string campaignShipId) => snapshots.Add(ship, new Entry(campaignShipId));

    /// <inheritdoc/>
    public bool Contains(Ship ship) => ship != null && snapshots.TryGetValue(ship, out _);

    /// <inheritdoc/>
    public string CampaignShipIdOf(Ship ship) =>
        ship != null && snapshots.TryGetValue(ship, out var entry) ? entry.CampaignShipId : null;

    /// <inheritdoc/>
    public void AddMitigatedDamage(Ship ship, float mitigatedDamage)
    {
        if (ship == null || mitigatedDamage <= 0f || !snapshots.TryGetValue(ship, out var entry)) return;

        entry.MitigatedDamage += mitigatedDamage;
    }

    /// <inheritdoc/>
    public float MitigatedDamageOf(Ship ship) =>
        ship != null && snapshots.TryGetValue(ship, out var entry) ? entry.MitigatedDamage : 0f;

    /// <summary>[Any thread] Whether the ship is a snapshot in the client container patches resolve from; false when none resolves.</summary>
    internal static bool IsSnapshotHere(Ship ship, out ICoopShipSnapshotRegistry snapshots) =>
        ContainerProvider.TryResolve(out snapshots) && snapshots.Contains(ship);

    // What the weak table holds for one snapshot: its campaign ship's registry id and the damage mitigated on it.
    private sealed class Entry
    {
        // Takes the campaign ship's registry id, null when the source had none.
        public Entry(string campaignShipId)
        {
            CampaignShipId = campaignShipId;
        }

        public string CampaignShipId { get; }
        public float MitigatedDamage { get; set; }
    }
}
