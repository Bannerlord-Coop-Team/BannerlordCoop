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

    private sealed class Entry
    {
        public Entry(Ship source, string campaignShipId)
        {
            Source = source;
            CampaignShipId = campaignShipId;
        }

        public Ship Source { get; }
        public string CampaignShipId { get; }
    }
}
