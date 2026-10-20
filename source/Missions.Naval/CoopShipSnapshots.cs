using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Naval;

namespace Missions.Naval;

/// <summary>
/// The detached mission ships <see cref="CoopShipSnapshotBuilder"/> built, with the campaign ship each one stands for.
/// Static because the campaign gates read it; the keys are weak, so a snapshot leaves with its mission.
/// </summary>
internal static class CoopShipSnapshots
{
    private static readonly ConditionalWeakTable<Ship, Entry> snapshots = new ConditionalWeakTable<Ship, Entry>();

    /// <summary>Records a snapshot of the party's own <paramref name="source"/>, or of another owner's ship without one.</summary>
    public static void Add(Ship ship, Ship source = null)
    {
        snapshots.Remove(ship);
        snapshots.Add(ship, new Entry(source));
    }

    public static bool Contains(Ship ship) => ship != null && snapshots.TryGetValue(ship, out _);

    /// <summary>The party's own campaign ship a snapshot was built from; null for another owner's ship.</summary>
    public static Ship SourceOf(Ship ship) => ship != null && snapshots.TryGetValue(ship, out var entry) ? entry.Source : null;

    // What the weak table holds for one snapshot: the own campaign ship it was built from, if any.
    private sealed class Entry
    {
        public Entry(Ship source)
        {
            Source = source;
        }

        public Ship Source { get; }
    }
}
