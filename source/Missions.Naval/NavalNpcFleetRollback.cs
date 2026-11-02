using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>[Game thread, battle host] Takes back what a failed AI fleet attempt left in the mission.</summary>
public interface INavalNpcFleetRollback
{
    /// <summary>
    /// Drops the attempt's troops still waiting in vanilla's crewing, then takes back its hulls with their crew. Repeatable: a
    /// call after a throw resumes the step that threw. Returns the hulls left for another client, or for a later call here.
    /// </summary>
    NpcHullHandover Rollback(NavalNpcFleetAttemptWork work);
}

/// <summary>
/// Undoes a failed attempt's crewing and hulls. Its pulled troops stay supplied, since the server's supply ledger never rewinds
/// and a battle tries its fleet once; the spawned crew withdraws alive with its hull.
/// </summary>
public sealed class NavalNpcFleetRollback : INavalNpcFleetRollback
{
    private readonly IBattleShipReplicator shipReplicator;

    // The battle's ship replicator, which announces and removes the attempt's hulls.
    public NavalNpcFleetRollback(IBattleShipReplicator shipReplicator)
    {
        this.shipReplicator = shipReplicator;
    }

    // Troops go first and the hulls wait for them, so no reinforcement spawns onto a hull while its removal is queued.
    public NpcHullHandover Rollback(NavalNpcFleetAttemptWork work)
    {
        if (!work.TroopsDropped)
        {
            // A handed-over take-back never crewed here, so it has no troops in this client's crewing.
            if (work.CrewingStarted) DropWaitingTroops(Mission.Current?.GetMissionBehavior<NavalAgentsLogic>(), work);
            work.MarkTroopsDropped();
        }

        if (work.ShipIds.Count > 0) return shipReplicator.RetractNpcShips(work.ShipIds, work.HostEpoch);
        return work.Hulls.Count > 0 ? shipReplicator.RetractNpcHulls(work.Hulls) : NpcHullHandover.None;
    }

    // Puts the reinforcement switch back and drops the attempt's troops vanilla still holds, reserved on its hulls or unassigned;
    // each part finds nothing left to drop when it runs again.
    private static void DropWaitingTroops(NavalAgentsLogic agentsLogic, NavalNpcFleetAttemptWork work)
    {
        if (agentsLogic == null) return;

        agentsLogic.SetSpawnReinforcementsOnTick(work.CrewSide, work.SpawnedOnTickBefore);
        foreach (var hull in work.Hulls)
        {
            if (hull is MissionShip ship && !ship.IsRemoved && ship.Team != null)
                agentsLogic.RemoveAllReservedTroopsFromShip(ship);
        }

        if (!agentsLogic.GetTeamAgents(work.CrewSide, out var teamAgents)) return;
        foreach (var origin in work.Troops)
        {
            if (teamAgents.DequeueUnassignedTroop(origin, out _)) teamAgents.RemoveTroopOriginAux(origin);
        }
    }
}
