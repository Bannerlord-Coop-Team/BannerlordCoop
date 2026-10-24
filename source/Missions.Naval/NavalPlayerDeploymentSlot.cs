using NavalDLC.Missions.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>The own party's index among the side's player parties; 0 keeps the vanilla position.</summary>
    public static int Rank { get; set; }

    /// <summary>The naval logic whose initial side plan is being made, so the plan patch can read its team range.</summary>
    public static DefaultNavalMissionLogic PlanningSide { get; set; }

    /// <summary>Clears the per-mission slot state.</summary>
    public static void Reset()
    {
        Rank = 0;
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

    /// <summary>The deepest team range any fleet can have, from every ship's deployment length in the game data.</summary>
    internal static float LongestTeamRange(IEnumerable<float> shipDeploymentLengths) =>
        TeamRangeMargin * shipDeploymentLengths.DefaultIfEmpty(0f).Max();

    /// <summary>
    /// The own team's spawn path offset: each preceding player gets the deepest possible fleet plus vanilla's gap, and the
    /// own fleet (it extends back from its offset by <paramref name="ownTeamRange"/>) stays on the path.
    /// </summary>
    internal static float ShiftSpawnPathOffset(float vanillaOffset, int rank, float ownTeamRange, float longestTeamRange,
        float pathStartOffset)
    {
        if (rank <= 0) return vanillaOffset;

        float shifted = vanillaOffset - (rank * (longestTeamRange + InterTeamGap));
        return Math.Min(vanillaOffset, Math.Max(shifted, pathStartOffset + ownTeamRange));
    }
}
