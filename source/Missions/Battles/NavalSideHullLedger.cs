using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace Missions.Battles;

/// <summary>[Game thread] The hulls that left each side, with their owner or to a capture, which the end check's retreat rule reads.</summary>
public interface INavalSideHullLedger
{
    /// <summary>How many hulls left <paramref name="side"/>.</summary>
    int DepartedHulls(BattleSideEnum side);

    /// <summary>
    /// Counts a hull as gone from <paramref name="side"/>. Each hull counts once per side, however many paths report it (its
    /// owner's departure, then a captor's record of it); an orphan's side is none and counts for no side.
    /// </summary>
    void CountDeparted(Guid shipId, BattleSideEnum side);
}

/// <inheritdoc cref="INavalSideHullLedger"/>
public class NavalSideHullLedger : INavalSideHullLedger
{
    private readonly Dictionary<BattleSideEnum, HashSet<Guid>> departed = new Dictionary<BattleSideEnum, HashSet<Guid>>();

    public int DepartedHulls(BattleSideEnum side) => departed.TryGetValue(side, out var ships) ? ships.Count : 0;

    public void CountDeparted(Guid shipId, BattleSideEnum side)
    {
        if (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender) return;

        if (!departed.TryGetValue(side, out var ships))
        {
            ships = new HashSet<Guid>();
            departed.Add(side, ships);
        }

        ships.Add(shipId);
    }
}
