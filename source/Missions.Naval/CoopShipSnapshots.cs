using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Naval;

namespace Missions.Naval;

/// <summary>
/// The detached mission ships <see cref="CoopShipSnapshotBuilder"/> built. Static because the campaign gates read it;
/// the keys are weak, so a snapshot leaves with its mission.
/// </summary>
internal static class CoopShipSnapshots
{
    private static readonly ConditionalWeakTable<Ship, object> snapshots = new ConditionalWeakTable<Ship, object>();
    private static readonly object marker = new object();

    public static void Add(Ship ship) => snapshots.GetValue(ship, _ => marker);

    public static bool Contains(Ship ship) => ship != null && snapshots.TryGetValue(ship, out _);
}
