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

    /// <summary>Records a snapshot of the party's own <paramref name="source"/>, or of another owner's ship by its registry id.</summary>
    public static void Add(Ship ship, Ship source = null, string campaignShipId = null)
    {
        snapshots.Remove(ship);
        snapshots.Add(ship, new Entry(source, campaignShipId));
    }

    public static bool Contains(Ship ship) => ship != null && snapshots.TryGetValue(ship, out _);

    /// <summary>The party's own campaign ship a snapshot was built from; null for another owner's ship.</summary>
    public static Ship SourceOf(Ship ship) => ship != null && snapshots.TryGetValue(ship, out var entry) ? entry.Source : null;

    /// <summary>The registry id another owner's record gave the snapshot's campaign ship; null for an own snapshot.</summary>
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

    // What the weak table holds for one snapshot: the own campaign ship it was built from or the id a record carried, and the
    // damage mitigated on it.
    private sealed class Entry
    {
        public Entry(Ship source, string campaignShipId)
        {
            Source = source;
            CampaignShipId = campaignShipId;
        }

        public Ship Source { get; }
        public string CampaignShipId { get; }
        public float MitigatedDamage { get; set; }
    }
}
