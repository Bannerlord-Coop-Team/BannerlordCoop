#if COOP_NAVAL
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.DividableTasks;
using Xunit;
using ThreatSeeker = TaleWorlds.MountAndBlade.RangedSiegeWeaponAi.ThreatSeeker;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// R51 B4: a hull that left mid-battle stayed in a pirate ballista's target list, and the ballista's next threat search read
/// its freed entity (AccessViolation in ThreatSeeker.GetAllThreats). No ballista may keep a target on a removed hull.
/// </summary>
public class NavalBallistaTargetsTests
{
    // R46: weapons list ship targets once, so a late hull refreshes every fielded hull's ballista but a removed one's.
    [Fact]
    public void WeaponAisOf_EveryFieldedHullsBallistaButRemovedOnes()
    {
        var fielded = ArmedHull();
        var spawned = ArmedHull();
        var removed = ArmedHull();
        removed.Hull._isRemoved = true;
        var unarmed = ShipTestHulls.Uninitialized<MissionShip>();

        var ais = NavalBallistaTargets.WeaponAisOf(new[] { fielded.Hull, spawned.Hull, removed.Hull, unarmed, null! });

        Assert.Equal(new[] { fielded.Ai, spawned.Ai }, ais);
    }

    // The ballista list, the threat search and the aim each read the target's entity, so all three lose the removed hull.
    [Fact]
    public void ForgetHull_DropsTheRemovedHullsWeaponAndHullTarget_FromEveryOtherBallista()
    {
        var removed = ArmedHull();
        var other = ArmedHull();
        var aiming = ArmedHull();
        var removedTarget = HullTarget(removed.Hull);
        var otherTarget = HullTarget(other.Hull);
        var keptThreat = Aim(otherTarget);
        var formationThreat = new Threat();

        Set(Seeker(aiming.Ai), "_potentialTargetObjects",
            new List<ITargetable> { otherTarget, (ITargetable)other.Weapon, removedTarget, (ITargetable)removed.Weapon });
        Set(Seeker(aiming.Ai), "_currentThreat", Aim(removedTarget));
        Set(aiming.Ai, "_target", Aim((ITargetable)removed.Weapon));
        Set(Search(aiming.Ai), "_threats", new List<Threat> { keptThreat, Aim(removedTarget), formationThreat });
        Set(Search(aiming.Ai), "_currentThreat", Aim((ITargetable)removed.Weapon));

        new NavalBallistaTargets().ForgetHull(new[] { other.Hull, aiming.Hull }, removed.Hull);

        Assert.Equal(new ITargetable[] { otherTarget, (ITargetable)other.Weapon },
            Get<List<ITargetable>>(Seeker(aiming.Ai), "_potentialTargetObjects"));
        Assert.Null(Get<Threat>(Seeker(aiming.Ai), "_currentThreat"));
        Assert.Null(Get<Threat>(aiming.Ai, "_target"));
        Assert.Equal(new[] { keptThreat, formationThreat }, Get<List<Threat>>(Search(aiming.Ai), "_threats"));
        Assert.Null(Get<Threat>(Search(aiming.Ai), "_currentThreat"));
    }

    // A threat on a hull that stays is kept, and a ballista that has not deployed yet (no list, no search) is left alone.
    [Fact]
    public void ForgetHull_KeepsTargetsOnHullsThatStay_AndSkipsUndeployedBallistas()
    {
        var removed = ArmedHull();
        var other = ArmedHull();
        var aiming = ArmedHull();
        var undeployed = ArmedHull();
        var kept = Aim(HullTarget(other.Hull));
        Set(aiming.Ai, "_target", kept);
        Set(Seeker(aiming.Ai), "_currentThreat", kept);

        new NavalBallistaTargets().ForgetHull(new[] { other.Hull, aiming.Hull, undeployed.Hull }, removed.Hull);

        Assert.Same(kept, Get<Threat>(aiming.Ai, "_target"));
        Assert.Same(kept, Get<Threat>(Seeker(aiming.Ai), "_currentThreat"));
    }

    // A constructor-skipped hull whose ballista AI has an empty threat seeker and threat search, as before its deployment.
    private static (MissionShip Hull, RangedSiegeWeapon Weapon, RangedSiegeWeaponAi Ai) ArmedHull()
    {
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        var weapon = ShipTestHulls.CreateWeapon();
        var ai = (RangedSiegeWeaponAi)RuntimeHelpers.GetUninitializedObject(typeof(BallistaAI));
        var seeker = (ThreatSeeker)RuntimeHelpers.GetUninitializedObject(typeof(ThreatSeeker));
        Set(seeker, "_getMostDangerousThreat", RuntimeHelpers.GetUninitializedObject(typeof(FindMostDangerousThreat)));
        Set(ai, "_threatSeeker", seeker);
        AccessTools.Field(typeof(UsableMachine), "_ai").SetValue(weapon, ai);
        hull.ShipSiegeWeapon = weapon;
        return (hull, weapon, ai);
    }

    // The hull's own target object, which a ballista lists next to the hull's weapon.
    private static ShipTargetMissionObject HullTarget(MissionShip hull)
    {
        var target = ShipTestHulls.Uninitialized<ShipTargetMissionObject>();
        target._ship = hull;
        return target;
    }

    // A threat aimed at one target.
    private static Threat Aim(ITargetable target) => new Threat { TargetableObject = target };

    // The ballista AI's threat seeker.
    private static ThreatSeeker Seeker(RangedSiegeWeaponAi ai) => Get<ThreatSeeker>(ai, "_threatSeeker");

    // The threat seeker's search for the most dangerous threat.
    private static FindMostDangerousThreat Search(RangedSiegeWeaponAi ai) => Get<FindMostDangerousThreat>(Seeker(ai), "_getMostDangerousThreat");

    // Writes a private field, which this test project does not publicize for TaleWorlds.MountAndBlade.
    private static void Set(object owner, string field, object value) => AccessTools.Field(owner.GetType(), field).SetValue(owner, value);

    // Reads a private field.
    private static T Get<T>(object owner, string field) => (T)AccessTools.Field(owner.GetType(), field).GetValue(owner);
}


#endif
