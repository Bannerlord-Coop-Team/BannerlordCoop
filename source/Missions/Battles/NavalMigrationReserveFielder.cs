using Common.Logging;
using GameInterface.Services.MapEvents.TroopSupply;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;

namespace Missions.Battles;

/// <summary>
/// [Game thread, battle host] Puts the reserve a naval host recovers onto the AI hulls it simulates. A departed host's queued
/// AI crew left with it; <see cref="IReinforcementFielder"/> recovers those troops from the roster, and here they join each
/// hull's vanilla reserve, which spawns them as its crew falls.
/// </summary>
public interface INavalMigrationReserveFielder
{
    /// <summary>[Game thread] Takes newly recovered reserve and queues what fits on the AI hulls simulated here.</summary>
    void Tick();

    /// <summary>
    /// [Game thread] Recovered reserve of a side that no hull holds yet, while an afloat AI hull of that side can take it.
    /// </summary>
    int PendingReserve(BattleSideEnum side);

    /// <summary>Recovered, pending and fielded counts (diagnostics).</summary>
    object Inspect();
}

/// <inheritdoc cref="INavalMigrationReserveFielder"/>
public class NavalMigrationReserveFielder : INavalMigrationReserveFielder
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalMigrationReserveFielder>();
    private static readonly BattleSideEnum[] Sides = { BattleSideEnum.Defender, BattleSideEnum.Attacker };

    private readonly IBattleSession session;
    private readonly IReinforcementFielder reinforcementFielder;
    private readonly INetworkShipRegistry registry;
    private readonly INavalShipEngine engine;
    private readonly INavalHullAuthority hullAuthority;
    private readonly List<CoopAgentOrigin>[] pending = { new List<CoopAgentOrigin>(), new List<CoopAgentOrigin>() };
    private int recovered;
    private int fielded;

    public NavalMigrationReserveFielder(IBattleSession session, IReinforcementFielder reinforcementFielder,
        INetworkShipRegistry registry, INavalShipEngine engine, INavalHullAuthority hullAuthority)
    {
        this.session = session;
        this.reinforcementFielder = reinforcementFielder;
        this.registry = registry;
        this.engine = engine;
        this.hullAuthority = hullAuthority;
    }

    public void Tick()
    {
        if (!session.IsLocalHost) return;

        foreach (var side in Sides)
        {
            var hulls = AfloatHostHulls(side);
            if (hulls.Count == 0) continue;

            var taken = reinforcementFielder.TakeMigrationReserve(side);
            if (taken.Count > 0)
            {
                pending[(int)side].AddRange(taken);
                recovered += taken.Count;
                Logger.Information("[NavalBattle] Recovered {Count} {Side} reserve troop(s) after a host change",
                    taken.Count, side);
            }

            DropLateReplayed(side);
            FieldPending(side, hulls.Where(ship => !hullAuthority.IsReplacing(ship.ShipId)).ToList());
        }
    }

    public int PendingReserve(BattleSideEnum side)
    {
        if (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender) return 0;

        var waiting = pending[(int)side];
        return waiting.Count > 0 && AfloatHostHulls(side).Count > 0 ? waiting.Count : 0;
    }

    // A troop can wait here across a hull replacement or a full hull, so one a late replay brought in meanwhile is dropped.
    private void DropLateReplayed(BattleSideEnum side)
    {
        int dropped = pending[(int)side].RemoveAll(reinforcementFielder.IsLateReplayed);
        if (dropped == 0) return;

        Logger.Information("[NavalBattle] Dropped {Count} pending {Side} reserve troop(s); a late replay already brought them in",
            dropped, side);
    }

    // Each hull takes its own party's troops first, then any party's, so one party's hulls do not fill up on another's.
    private void FieldPending(BattleSideEnum side, List<NetworkShipInfo> hulls)
    {
        var waiting = pending[(int)side];
        if (waiting.Count == 0 || hulls.Count == 0) return;

        int added = 0;
        foreach (var ship in hulls)
            added += FieldOnto(ship, waiting, origin => origin.MapEventPartyId == ship.MapEventPartyId);
        foreach (var ship in hulls)
            added += FieldOnto(ship, waiting, _ => true);

        if (added == 0) return;

        fielded += added;
        Logger.Information("[NavalBattle] Queued {Count} recovered {Side} troop(s) on {Hulls} hull(s); {Pending} still waiting",
            added, side, hulls.Count, waiting.Count);
    }

    private int FieldOnto(NetworkShipInfo ship, List<CoopAgentOrigin> waiting, Func<CoopAgentOrigin, bool> matches)
    {
        var origins = waiting.Where(matches).ToList();
        if (origins.Count == 0) return 0;

        int added = engine.AddHullReserve(ship.Hull, origins);
        var queued = new HashSet<CoopAgentOrigin>(origins.Take(added));
        waiting.RemoveAll(queued.Contains);
        return added;
    }

    private List<NetworkShipInfo> AfloatHostHulls(BattleSideEnum side) => registry.Ships
        .Where(ship => ship.FollowsBattleHost && session.IsOwn(ship.CurrentAuthority) && !engine.IsHullRemoved(ship.Hull)
            && !engine.IsSinking(ship.Hull) && engine.SideOf(ship.Hull) == side)
        .ToList();

    public object Inspect() => new
    {
        recovered,
        fielded,
        pendingAttacker = pending[(int)BattleSideEnum.Attacker].Count,
        pendingDefender = pending[(int)BattleSideEnum.Defender].Count,
    };
}
