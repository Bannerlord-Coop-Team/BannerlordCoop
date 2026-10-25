using System.Collections.Generic;
using System.Linq;
using NavalDLC.Missions.AI.TeamAI;
using NavalDLC.Missions.Objects;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] Deploys the hulls spawned after deployment ended, which vanilla's deployment pass never reached, then on the
/// mission tick refreshes the ballistas' targets and re-applies the tactic of a formation a capture left without behaviours.
/// </summary>
public interface INavalLateHullDeployment
{
    /// <summary>Runs vanilla's deployment pass on a hull spawned after deployment, and queues its formation and the targets.</summary>
    void Deploy(MissionShip hull);

    /// <summary>[Mission tick] Refreshes the ballista targets of <paramref name="ships"/> and the queued formations' tactics.</summary>
    void Apply(IEnumerable<MissionShip> ships);
}

/// <summary>Deploys late hulls as they spawn and defers what reads the team AI to the mission tick.</summary>
public class NavalLateHullDeployment : INavalLateHullDeployment
{
    private readonly List<Formation> joined = new List<Formation>();
    private bool targetsStale;

    /// <summary>How many formations wait for the next check (diagnostics, tests).</summary>
    internal int Noted => joined.Count;

    // Runs once per hull: vanilla's removed-hull pool is emptied at deployment end, so no late hull has had the pass, the
    // non-repeatable rope machine setup included.
    public void Deploy(MissionShip hull)
    {
        hull.OnDeploymentFinished();
        targetsStale = true;
        Note(hull.Formation);
    }

    // Queues a formation once per tick; the async team tick may be reordering its behaviours until then.
    internal void Note(Formation formation)
    {
        if (formation != null && !joined.Contains(formation)) joined.Add(formation);
    }

    // Refreshes the targets once however many hulls deployed, checks every queued formation, then starts the next tick empty.
    public void Apply(IEnumerable<MissionShip> ships)
    {
        if (targetsStale)
        {
            // A ballista lists its ship targets once, at its own deployment (ThreatSeeker.InitializeTargetableObjects).
            foreach (var ai in WeaponAisToRefresh(ships)) ai.InitializeThreatSeeker();
            targetsStale = false;
        }

        if (joined.Count == 0) return;

        foreach (var formation in joined) Refresh(formation);
        joined.Clear();
    }

    /// <summary>The ranged weapon AIs of the hulls still in the mission.</summary>
    internal static IEnumerable<RangedSiegeWeaponAi> WeaponAisToRefresh(IEnumerable<MissionShip> ships) =>
        ships.Where(ship => ship != null && !ship.IsRemoved)
            .Select(ship => ship.ShipSiegeWeapon?.Ai as RangedSiegeWeaponAi)
            .Where(ai => ai != null);

    // The tactic re-weights formations only when their count or order changes, which swimmers already in the formation hide,
    // and walks a ship list cached 5 s; so the lists expire, as vanilla's ManageFormationCounts does, and the tactic re-applies.
    private static void Refresh(Formation formation)
    {
        if (formation.AI == null || formation.Team?.TeamAI is not TeamAINavalComponent teamAi) return;
        if (!NeedsTacticReapply(formation.CountOfUnits, formation.AI._behaviors.Any(behavior => behavior.WeightFactor > 1E-07f))) return;

        teamAi.TeamNavalQuerySystem.ForceExpireSameSideShipLists();
        if (teamAi.CurrentTactic != null) teamAi.CurrentTactic.IsTacticReapplyNeeded = true;
    }

    /// <summary>Whether a hull joined a formation with units whose behaviours all weigh nothing, as a capture leaves them.</summary>
    internal static bool NeedsTacticReapply(int units, bool anyBehaviorWeighted) => units > 0 && !anyBehaviorWeighted;
}
