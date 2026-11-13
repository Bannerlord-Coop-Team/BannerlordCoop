using NavalDLC.Missions.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Missions.Naval;

/// <summary>
/// The spawn path slot math for a side's fleets: each player's by rank and the allied AI fleet's ahead of them, which every
/// client derives alike. Stateless; the own rank lives in <see cref="INavalPlayerDeploymentSlotTracker"/>.
/// </summary>
internal static class NavalPlayerDeploymentSlot
{
    // DefaultNavalMissionLogic.MakeDeploymentPlansForSide's gap between a side's teams.
    internal const float InterTeamGap = 32f;

    // DefaultNavalMissionLogic.GetTeamSpawnPathOffsetRange's margin on a team's longest ship deployment length.
    internal const float TeamRangeMargin = 1.1f;

    // NavalShipsLogic.ComputeSpawnPathDeploymentOffset's distance between the two sides for a battle of two ships or fewer.
    internal const float SmallBattleSideDistance = 400f;

    /// <summary>How many player parties precede <paramref name="own"/> in the side's party order.</summary>
    internal static int RankOf<T>(IEnumerable<T> sideParties, T own, Func<T, bool> isPlayerParty) where T : class
    {
        int rank = 0;
        foreach (var party in sideParties)
        {
            if (ReferenceEquals(party, own)) return rank;
            if (isPlayerParty(party)) rank++;
        }

        return 0;
    }

    /// <summary>How many of the side's parties are player parties.</summary>
    internal static int PlayerCountOf<T>(IEnumerable<T> sideParties, Func<T, bool> isPlayerParty) =>
        sideParties.Count(isPlayerParty);

    /// <summary>The deepest team range any fleet can have, from every ship's deployment length in the game data.</summary>
    internal static float LongestTeamRange(IEnumerable<float> shipDeploymentLengths) =>
        TeamRangeMargin * shipDeploymentLengths.DefaultIfEmpty(0f).Max();

    /// <summary>[Game thread] The deepest team range of the game's ships.</summary>
    internal static float LongestTeamRangeOfGame() =>
        LongestTeamRange(MBObjectManager.Instance.GetObjectTypeList<MissionShipObject>().Select(ship => ship.DeploymentArea.y));

    /// <summary>
    /// Vanilla's side deployment offset for a battle of two ships or fewer. Vanilla deepens it with the ships it assigned, and in
    /// coop each client assigns only its own fleet, so a side with several players takes this value every client shares.
    /// </summary>
    internal static float SharedDeploymentOffset(float pathLength) => (-Math.Min(pathLength, SmallBattleSideDistance) / 2f) + 1f;

    /// <summary>The own fleet's slot at <paramref name="rank"/> on the side's spawn path.</summary>
    internal static NavalSideSlot SlotFor(SpawnPathData spawnPath, int rank, float longestTeamRange)
    {
        float[] slots = SlotsOnPath(spawnPath, rank + 1, longestTeamRange, out _, out var target, out bool fits);
        return new NavalSideSlot(slots[rank], target, fits);
    }

    /// <summary>The allied AI fleet's slot on the side's spawn path, a gap ahead of the front player slot.</summary>
    internal static NavalSideSlot AllySlotFor(SpawnPathData spawnPath, float longestTeamRange)
    {
        SlotsOnPath(spawnPath, 0, longestTeamRange, out float allySlot, out var target, out bool fits);
        return new NavalSideSlot(allySlot, target, fits);
    }

    // The side's slots from vanilla's base for the shared offset, kept a fleet and a gap short of the opposing base.
    private static float[] SlotsOnPath(SpawnPathData spawnPath, int ranks, float range, out float allySlot, out float target,
        out bool fits)
    {
        DefaultBattleMissionAgentSpawnLogic.ComputeDeploymentBaseOffsets(spawnPath, SharedDeploymentOffset(spawnPath.PathLength),
            out float sideBase, out target);
        var segments = Enumerable.Range(0, spawnPath.FreeSegmentCount).Select(spawnPath.GetFreeSegment).ToList();
        return Slots(segments, sideBase, target - range - InterTeamGap, range, ranks, out allySlot, out fits);
    }

    /// <summary>
    /// Each rank's front offset and the allied fleet's. Rank 0 takes vanilla's slot at the base, the next ranks go behind it over
    /// the free segments as vanilla places teams, and once the path runs out behind they go ahead of the allied slot, which is a
    /// gap ahead of rank 0. A slot depends only on its rank, so clients that count different players agree. Nothing goes past
    /// <paramref name="forwardLimit"/>; a slot that had to is clamped there and <paramref name="fits"/> is false.
    /// </summary>
    internal static float[] Slots(IReadOnlyList<(float Start, float End)> freeSegments, float sideBase, float forwardLimit,
        float range, int ranks, out float allySlot, out bool fits)
    {
        fits = true;
        float front = TryPlaceBehind(freeSegments, sideBase, range, out float behindBase)
            ? behindBase
            : PlaceAhead(freeSegments, freeSegments.Select(segment => segment.Start).DefaultIfEmpty(sideBase).Min(), range, forwardLimit,
                ref fits);
        allySlot = PlaceAhead(freeSegments, front + InterTeamGap, range, forwardLimit, ref fits);

        var slots = new float[ranks];
        float behindCursor = front - range - InterTeamGap;
        float aheadRear = allySlot + InterTeamGap;
        bool roomBehind = true;
        for (int rank = 0; rank < ranks; rank++)
        {
            if (rank == 0)
            {
                slots[rank] = front;
                continue;
            }

            roomBehind = roomBehind && TryPlaceBehind(freeSegments, behindCursor, range, out slots[rank]);
            if (roomBehind)
            {
                behindCursor = slots[rank] - range - InterTeamGap;
                continue;
            }

            slots[rank] = PlaceAhead(freeSegments, aheadRear, range, forwardLimit, ref fits);
            aheadRear = slots[rank] + InterTeamGap;
        }

        return slots;
    }

    // Vanilla's backward search (TryPlaceTeamsOneSide): the last free segment before the cursor with room for the fleet.
    private static bool TryPlaceBehind(IReadOnlyList<(float Start, float End)> freeSegments, float cursor, float range,
        out float offset)
    {
        for (int index = freeSegments.Count - 1; index >= 0; index--)
        {
            var (start, end) = freeSegments[index];
            if (start >= cursor) continue;

            offset = Math.Min(end, cursor);
            if (offset - start >= range) return true;
        }

        offset = cursor;
        return false;
    }

    // Vanilla's forward search: the first free segment past the rear with room for the fleet, clamped at the forward limit.
    private static float PlaceAhead(IReadOnlyList<(float Start, float End)> freeSegments, float rear, float range, float forwardLimit,
        ref bool fits)
    {
        float offset = rear + range;
        bool placed = false;
        foreach (var (start, end) in freeSegments)
        {
            if (end <= rear) continue;

            float from = Math.Max(start, rear);
            if (end - from < range) continue;

            offset = from + range;
            placed = true;
            break;
        }

        if (placed && offset <= forwardLimit) return offset;

        fits = false;
        return Math.Min(offset, forwardLimit);
    }
}
