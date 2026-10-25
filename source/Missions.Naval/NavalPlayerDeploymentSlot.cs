using NavalDLC.Missions.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] The spawn path slot of this client's own fleet among its side's player parties, which every client
/// plans at vanilla's first slot. Static because the deployment planning patches read it.
/// </summary>
internal static class NavalPlayerDeploymentSlot
{
    // DefaultNavalMissionLogic.MakeDeploymentPlansForSide's gap between a side's teams.
    internal const float InterTeamGap = 32f;

    // DefaultNavalMissionLogic.GetTeamSpawnPathOffsetRange's margin on a team's longest ship deployment length.
    internal const float TeamRangeMargin = 1.1f;

    // NavalShipsLogic.ComputeSpawnPathDeploymentOffset's distance between the two sides for a battle of two ships or fewer.
    internal const float SmallBattleSideDistance = 400f;

    // Float slack for the layout fit check; vanilla's placement adds and subtracts the same ranges.
    private const float FitTolerance = 0.01f;

    /// <summary>The own party's index among the side's player parties; 0 is the front slot.</summary>
    public static int Rank { get; set; }

    /// <summary>How many player parties fight on the own side, the own one included; one or none keeps vanilla's plan.</summary>
    public static int PlayerCount { get; set; }

    /// <summary>The naval logic whose initial side plan is being made, so the plan patch can read its team range.</summary>
    public static DefaultNavalMissionLogic PlanningSide { get; set; }

    /// <summary>Clears the per-mission slot state.</summary>
    public static void Reset()
    {
        Rank = 0;
        PlayerCount = 0;
        PlanningSide = null;
    }

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

    /// <summary>
    /// Vanilla's side deployment offset for a battle of two ships or fewer. Vanilla deepens it with the ships it assigned, and in
    /// coop each client assigns only its own fleet, so a side with several players takes this value every client shares.
    /// </summary>
    internal static float SharedDeploymentOffset(float pathLength) => (-Math.Min(pathLength, SmallBattleSideDistance) / 2f) + 1f;

    /// <summary>
    /// The own fleet's offset at <paramref name="rank"/>: vanilla's side base from the shared offset, then the side's layout,
    /// so every client derives the same one. Also gives the base and whether the layout fits the path.
    /// </summary>
    internal static float SlotFor(SpawnPathData spawnPath, int rank, int playerCount, float longestTeamRange, out float sideBase,
        out bool fits)
    {
        DefaultBattleMissionAgentSpawnLogic.ComputeDeploymentBaseOffsets(spawnPath, SharedDeploymentOffset(spawnPath.PathLength),
            out sideBase, out _);
        var layout = PlaceSideFleets(spawnPath, sideBase, playerCount, longestTeamRange);
        // SpawnPathData.ClampPathOffset keeps a relative offset at or above -PivotOffset, the start of the path.
        fits = FitsPath(layout, longestTeamRange, -spawnPath.PivotOffset);
        return layout[rank];
    }

    /// <summary>
    /// Every player fleet's front offset by rank, from vanilla's team placement with each fleet as deep as the deepest one,
    /// so all clients derive one layout and a path with no room behind the base packs the fleets forward like vanilla.
    /// </summary>
    internal static float[] PlaceSideFleets(SpawnPathData spawnPath, float deploymentBaseOffset, int playerCount,
        float longestTeamRange)
    {
        var teamRanges = Enumerable.Repeat(longestTeamRange, playerCount).ToArray();
        DefaultBattleMissionAgentSpawnLogic.ComputeTeamDeploymentOffsets(spawnPath, deploymentBaseOffset, InterTeamGap,
            teamRanges, out var offsets);
        return offsets;
    }

    /// <summary>Whether every fleet of the layout lies on the path and keeps vanilla's gap to the fleet ahead of it.</summary>
    internal static bool FitsPath(IReadOnlyList<float> layout, float longestTeamRange, float pathStartOffset)
    {
        for (int rank = 0; rank < layout.Count; rank++)
        {
            if (layout[rank] - longestTeamRange < pathStartOffset - FitTolerance) return false;
            if (rank > 0 && layout[rank - 1] - longestTeamRange - layout[rank] < InterTeamGap - FitTolerance) return false;
        }

        return true;
    }
}
