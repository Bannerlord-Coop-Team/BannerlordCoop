using Missions.Messages;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// [Game thread] Gathers a side's <see cref="NavalSideTally"/> from this client's hulls, their owner-reported reserve and the
/// reserve a promoted host still holds; the naval end check and the E2E scenarios count through it.
/// </summary>
public interface INavalSideTallyReader
{
    /// <summary>
    /// [Game thread] Counts <paramref name="side"/>'s hulls, with <paramref name="liveHumans"/> for the side and each hull's
    /// live crew from <paramref name="crewByHull"/> (none for a hull it doesn't list).
    /// </summary>
    NavalSideTally Count(BattleSideEnum side, int liveHumans, IReadOnlyDictionary<MissionObject, int> crewByHull);

    /// <summary>[Game thread] The live human crew of each hull among <paramref name="agents"/>, by the hull their formation crews.</summary>
    IReadOnlyDictionary<MissionObject, int> CrewByHull(IEnumerable<Agent> agents);
}

/// <inheritdoc cref="INavalSideTallyReader"/>
public class NavalSideTallyReader : INavalSideTallyReader
{
    /// <summary>No hull's crew, for the depletion check, which reads only live humans and reserve.</summary>
    public static readonly IReadOnlyDictionary<MissionObject, int> NoCrew = new Dictionary<MissionObject, int>();

    private readonly INavalShipEngine engine;
    private readonly IBattleShipConditionStream conditions;
    private readonly INavalMigrationReserveFielder migrationReserve;

    // Takes the engine that reads the hulls, the condition stream holding each hull's reported reserve and the migration reserve.
    public NavalSideTallyReader(INavalShipEngine engine, IBattleShipConditionStream conditions,
        INavalMigrationReserveFielder migrationReserve)
    {
        this.engine = engine;
        this.conditions = conditions;
        this.migrationReserve = migrationReserve;
    }

    // A capture's orphan has no team, so its side reads as none and no side counts it.
    public NavalSideTally Count(BattleSideEnum side, int liveHumans, IReadOnlyDictionary<MissionObject, int> crewByHull)
    {
        var hulls = new List<NavalHullCount>();
        foreach (var hull in engine.Hulls)
        {
            int crew = crewByHull.TryGetValue(hull, out int count) ? count : 0;
            bool isSunk = engine.ReadCondition(hull).SinkingState == BattleShipCondition.Sunk;
            hulls.Add(new NavalHullCount(engine.SideOf(hull), isSunk, crew, conditions.HullReserve(hull)));
        }

        // Reserve a promoted host recovered still waits for a hull, yet keeps the side alive.
        return NavalSideTally.Count(side, liveHumans, hulls, migrationReserve.PendingReserve(side));
    }

    // A copy's crew are puppets in the hull's formation, which the copy's NavalShipAgents never tracks.
    public IReadOnlyDictionary<MissionObject, int> CrewByHull(IEnumerable<Agent> agents)
    {
        var counts = new Dictionary<MissionObject, int>();
        foreach (var agent in agents)
        {
            if (!agent.IsHuman || !agent.IsActive()) continue;

            var hull = engine.CrewHullOf(agent);
            if (hull == null) continue;

            counts.TryGetValue(hull, out int count);
            counts[hull] = count + 1;
        }

        return counts;
    }
}
