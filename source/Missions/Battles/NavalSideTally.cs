using System.Collections.Generic;
using TaleWorlds.Core;

namespace Missions.Battles;

/// <summary>One hull as the naval end check counts it: its side, whether it sank, its live crew and its fieldable reserve.</summary>
public readonly struct NavalHullCount
{
    public NavalHullCount(BattleSideEnum side, bool isSunk, int crew, int reserve)
    {
        Side = side;
        IsSunk = isSunk;
        Crew = crew;
        Reserve = reserve;
    }

    public BattleSideEnum Side { get; }
    public bool IsSunk { get; }
    public int Crew { get; }
    public int Reserve { get; }
}

/// <summary>
/// One side's counts for the coop naval end check, decided by vanilla <c>NavalBattleEndLogic</c>'s depletion and hull
/// out-of-action rules. NavalDLC-free, so the end logic and the naval E2E scenarios count the same way.
/// </summary>
public sealed class NavalSideTally
{
    /// <summary>Vanilla's <c>NavalBattleEndLogic.MinTroopCountForOutOfActionCheck</c>.</summary>
    public const int MinTroopCountForOutOfAction = 3;

    /// <summary>Every live human of the side's teams, swimmers included, as vanilla's spawn logic counts them.</summary>
    public int LiveHumans { get; private set; }

    /// <summary>Live crew of the side's afloat hulls.</summary>
    public int CrewAboard { get; private set; }

    public int Hulls { get; private set; }
    public int HullsAfloat { get; private set; }
    public int HullsOutOfAction { get; private set; }

    /// <summary>Fieldable reserve on the side's afloat hulls, plus reserve still waiting for a hull.</summary>
    public int Reserve { get; private set; }

    /// <summary>Vanilla's spawn-logic rule: no live troops and no fieldable reserve.</summary>
    public bool IsDepleted => LiveHumans + Reserve == 0;

    /// <summary>A side is out of action only when it has hulls and every one of them is.</summary>
    public bool IsOutOfAction => Hulls > 0 && HullsOutOfAction == Hulls;

    /// <summary>
    /// Counts <paramref name="side"/>'s hulls. A sunk hull is out of action and its reserve can no longer field, while its
    /// swimming crew stay in <paramref name="liveHumans"/> until they drown.
    /// </summary>
    public static NavalSideTally Count(BattleSideEnum side, int liveHumans, IEnumerable<NavalHullCount> hulls, int pendingReserve = 0)
    {
        var tally = new NavalSideTally { LiveHumans = liveHumans, Reserve = pendingReserve };
        foreach (var hull in hulls)
        {
            if (hull.Side != side) continue;

            int reserve = hull.IsSunk ? 0 : hull.Reserve;
            tally.Hulls++;
            if (!hull.IsSunk)
            {
                tally.HullsAfloat++;
                tally.CrewAboard += hull.Crew;
            }

            if (IsHullOutOfAction(hull.IsSunk, hull.Crew, reserve)) tally.HullsOutOfAction++;
            tally.Reserve += reserve;
        }

        return tally;
    }

    /// <summary>Vanilla's per-hull rule: sunk, or at most three troops aboard and in reserve.</summary>
    public static bool IsHullOutOfAction(bool isSunk, int crew, int reserve) =>
        isSunk || crew + reserve <= MinTroopCountForOutOfAction;
}
