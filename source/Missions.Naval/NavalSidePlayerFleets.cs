using System;

namespace Missions.Naval;

/// <summary>
/// The players' fleets a side's AI share left room for: those players' party ids and their fielded hulls together, and how many
/// player parties the side had, also one whose id couldn't be read.
/// </summary>
public readonly struct NavalSidePlayerFleets
{
    public readonly string[] PartyIds;
    public readonly int Hulls;
    public readonly int PlayerCount;

    // Holds the counted players' party ids and their hulls, and the side's player parties.
    public NavalSidePlayerFleets(string[] partyIds, int hulls, int playerCount)
    {
        PartyIds = partyIds;
        Hulls = hulls;
        PlayerCount = playerCount;
    }

    /// <summary>No player counted, as an attempt that failed before its share was worked out reports.</summary>
    public static NavalSidePlayerFleets None => new NavalSidePlayerFleets(Array.Empty<string>(), 0, 0);
}
