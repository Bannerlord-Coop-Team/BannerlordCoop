using NavalDLC.Missions.MissionLogics;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Naval;

namespace Missions.Naval;

/// <summary>
/// A player's own fleet at sea: the campaign ships its launcher fields, its share, and the other players' share (invalid when it's
/// alone), which leaves the side's AI parties out.
/// </summary>
public readonly struct NavalPlayerFleet
{
    public readonly IReadOnlyList<Ship> Ships;
    public readonly NavalShipDeploymentLimit PlayerLimit;
    public readonly NavalShipDeploymentLimit AllyLimit;

    // Holds the fleet's ships with its own and the other players' deployment limits.
    public NavalPlayerFleet(IReadOnlyList<Ship> ships, NavalShipDeploymentLimit playerLimit, NavalShipDeploymentLimit allyLimit)
    {
        Ships = ships;
        PlayerLimit = playerLimit;
        AllyLimit = allyLimit;
    }

    /// <summary>How many hulls the fleet puts in the battle: its ships, at most its share of the side.</summary>
    public int FieldedCount => Math.Min(Ships.Count, PlayerLimit.NetDeploymentLimit);
}
