using System;

namespace Missions.Naval;

/// <summary>The players' fleets a side's AI share left room for: those players' party ids and their fielded hulls together.</summary>
public readonly struct NavalSidePlayerFleets
{
    public readonly string[] PartyIds;
    public readonly int Hulls;

    // Holds the counted players' party ids and their hulls.
    public NavalSidePlayerFleets(string[] partyIds, int hulls)
    {
        PartyIds = partyIds;
        Hulls = hulls;
    }

    /// <summary>No player counted, as an attempt that failed before its share was worked out reports.</summary>
    public static NavalSidePlayerFleets None => new NavalSidePlayerFleets(Array.Empty<string>(), 0);
}
