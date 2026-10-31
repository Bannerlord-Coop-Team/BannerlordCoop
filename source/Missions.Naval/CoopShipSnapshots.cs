using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Naval;

namespace Missions.Naval;

/// <summary>
/// The detached mission ships <see cref="CoopShipSnapshotBuilder"/> built, with the campaign ship each one stands for and the
/// damage its upgrades mitigated. Static because the campaign gates read it; the keys are weak, so a snapshot leaves with its mission.
/// </summary>
internal static class CoopShipSnapshots
{
    private static readonly ConditionalWeakTable<Ship, Entry> snapshots = new ConditionalWeakTable<Ship, Entry>();

    /// <summary>Records a new snapshot of the campaign ship with registry id <paramref name="campaignShipId"/>.</summary>
    public static void Add(Ship ship, string campaignShipId) => snapshots.Add(ship, new Entry(campaignShipId));

    /// <summary>True for a ship the builder made, which the campaign gates keep off the campaign.</summary>
    public static bool Contains(Ship ship) => ship != null && snapshots.TryGetValue(ship, out _);

    /// <summary>The registry id of the campaign ship a snapshot stands for; null for any other ship or an unregistered source.</summary>
    public static string CampaignShipIdOf(Ship ship) =>
        ship != null && snapshots.TryGetValue(ship, out var entry) ? entry.CampaignShipId : null;

    /// <summary>Adds damage the snapshot's upgrades mitigated on a hit, which vanilla would have turned into Boatswain XP.</summary>
    public static void AddMitigatedDamage(Ship ship, float mitigatedDamage)
    {
        if (ship == null || mitigatedDamage <= 0f || !snapshots.TryGetValue(ship, out var entry)) return;

        entry.MitigatedDamage += mitigatedDamage;
    }

    /// <summary>The damage the snapshot's upgrades mitigated so far in this mission; 0 for any other ship.</summary>
    public static float MitigatedDamageOf(Ship ship) =>
        ship != null && snapshots.TryGetValue(ship, out var entry) ? entry.MitigatedDamage : 0f;

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
