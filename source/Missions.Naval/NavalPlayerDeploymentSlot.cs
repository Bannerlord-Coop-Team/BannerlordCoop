using NavalDLC.Missions.MissionLogics;
using System;
using System.Collections.Generic;

namespace Missions.Naval;

/// <summary>
/// [Game thread] Where this client's own fleet deploys among the player parties of its side. Every client deploys its
/// fleet on its own PlayerTeam, which vanilla plans at the same spot; the n-th player party of the side (in the
/// replicated side order every client shares) is pushed back along the spawn path by n team depths, the way vanilla
/// stacks a side's teams. Static because the deployment planning patches read it.
/// </summary>
internal static class NavalPlayerDeploymentSlot
{
    // DefaultNavalMissionLogic.MakeDeploymentPlansForSide's gap between a side's teams.
    internal const float InterTeamGap = 32f;

    /// <summary>The own party's index among the side's player parties; 0 keeps the vanilla position.</summary>
    public static int Rank { get; set; }

    /// <summary>The naval logic whose initial side plan is being made, so the plan patch can read its team range.</summary>
    public static DefaultNavalMissionLogic PlanningSide { get; set; }

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

    /// <summary>The own team's spawn path offset, one team depth plus vanilla's gap behind per preceding player.</summary>
    internal static float ShiftSpawnPathOffset(float vanillaOffset, int rank, float teamRange) =>
        vanillaOffset - (rank * (teamRange + InterTeamGap));
}
