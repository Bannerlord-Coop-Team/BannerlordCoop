using System;
using System.Collections.Generic;
using System.Linq;
using NavalDLC.Missions.Objects;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>[Game thread] Keeps the ballistas' ship targets in step with the hulls in the mission.</summary>
public interface INavalBallistaTargets
{
    /// <summary>Has every ballista of <paramref name="ships"/> list its targets again, so it sees the hulls spawned since.</summary>
    void Refresh(IEnumerable<MissionShip> ships);

    /// <summary>Drops every target on <paramref name="removed"/> from the ballistas of <paramref name="ships"/>.</summary>
    void ForgetHull(IEnumerable<MissionShip> ships, MissionShip removed);
}

/// <summary>
/// Lists and prunes the ballistas' ship targets. A ballista keeps a listed target until it is disabled, and a hull leaving
/// mid-battle frees its entity still enabled, so a kept target would read freed memory (R51 B4).
/// </summary>
public class NavalBallistaTargets : INavalBallistaTargets
{
    // A ballista lists its targets once, at its own deployment (ThreatSeeker.InitializeTargetableObjects).
    public void Refresh(IEnumerable<MissionShip> ships)
    {
        foreach (var ai in WeaponAisOf(ships)) ai.InitializeThreatSeeker();
    }

    // Runs from ShipRemovedEvent, which vanilla's RemoveShipAux fires before it frees the hull's entity.
    public void ForgetHull(IEnumerable<MissionShip> ships, MissionShip removed)
    {
        foreach (var ai in WeaponAisOf(ships)) Forget(ai, target => IsOn(target, removed));
    }

    /// <summary>The ranged weapon AIs of the hulls still in the mission.</summary>
    internal static IEnumerable<RangedSiegeWeaponAi> WeaponAisOf(IEnumerable<MissionShip> ships) =>
        ships.Where(ship => ship != null && !ship.IsRemoved)
            .Select(ship => ship.ShipSiegeWeapon?.Ai as RangedSiegeWeaponAi)
            .Where(ai => ai != null);

    /// <summary>Whether a ship target is <paramref name="hull"/>'s weapon or the hull itself, the two a hull carries.</summary>
    internal static bool IsOn(ITargetable target, MissionShip hull) =>
        target == hull.ShipSiegeWeapon || target is ShipTargetMissionObject hullTarget && hullTarget._ship == hull;

    /// <summary>
    /// Drops the gone targets from a ballista's list, its threat search and its aim, each of which reads the target's entity
    /// (GetAllThreats, CanShootAtThreat, AimAtThreat).
    /// </summary>
    internal static void Forget(RangedSiegeWeaponAi ai, Predicate<ITargetable> isGone)
    {
        bool IsGone(Threat threat) => threat?.TargetableObject != null && isGone(threat.TargetableObject);

        var seeker = ai._threatSeeker;
        // Null until the ballista's own deployment lists its targets.
        seeker._potentialTargetObjects?.RemoveAll(isGone);
        if (IsGone(seeker._currentThreat)) seeker._currentThreat = null;
        if (IsGone(ai._target)) ai._target = null;

        var search = seeker._getMostDangerousThreat;
        search._threats?.RemoveAll(IsGone);
        if (IsGone(search._currentThreat)) search._currentThreat = null;
    }
}
