#if COOP_NAVAL
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using System;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Late hulls get vanilla's deployment pass, which runs only on hulls spawned before deployment ends.</summary>
public class NavalLateHullDeploymentTests
{
    // Late hulls get vanilla's own deployment pass, and the ballistas fielded before them re-read their targets.
    [Fact]
    public void Target_VanillaDeploymentPassExists()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(MissionShip), nameof(MissionShip.OnDeploymentFinished)));
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(RangedSiegeWeaponAi), nameof(RangedSiegeWeaponAi.InitializeThreatSeeker)));
    }

    // R46: weapons list ship targets once, so a late hull refreshes every fielded hull's ballista but a removed one's.
    [Fact]
    public void WeaponAisToRefresh_EveryFieldedHullsBallistaButRemovedOnes()
    {
        var (fielded, fieldedAi) = ArmedHull();
        var (spawned, spawnedAi) = ArmedHull();
        var (removed, _) = ArmedHull();
        removed._isRemoved = true;
        var unarmed = ShipTestHulls.Uninitialized<MissionShip>();

        var refreshed = NavalLateHullDeployment.WeaponAisToRefresh(new[] { fielded, spawned, removed, unarmed, null });

        Assert.Equal(new[] { fieldedAi, spawnedAi }, refreshed);
    }

    // A formation noted twice is checked once; one without a formation AI is left alone, and the next tick starts empty.
    [Fact]
    public void Apply_ChecksEachNotedFormationOnce_ThenForgetsThem()
    {
        var deployment = new NavalLateHullDeployment();
        var formation = (Formation)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Formation));

        deployment.Note(null!);
        deployment.Note(formation);
        deployment.Note(formation);
        Assert.Equal(1, deployment.Noted);

        deployment.Apply(Array.Empty<MissionShip>());

        Assert.Equal(0, deployment.Noted);
    }

    // R48 review: a hull joining a formation a capture left with units and no behaviour weight asks the tactic to re-apply.
    [Theory]
    [InlineData(9, false, true)]
    [InlineData(0, false, false)]
    [InlineData(9, true, false)]
    public void NeedsTacticReapply_OnlyForUnitsWithEveryBehaviourWeightless(int units, bool anyWeighted, bool expected)
    {
        Assert.Equal(expected, NavalLateHullDeployment.NeedsTacticReapply(units, anyWeighted));
    }

    // A constructor-skipped hull carrying a ballista whose AI is already built.
    private static (MissionShip Hull, RangedSiegeWeaponAi Ai) ArmedHull()
    {
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        var weapon = ShipTestHulls.CreateWeapon();
        var ai = (RangedSiegeWeaponAi)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(BallistaAI));
        AccessTools.Field(typeof(UsableMachine), "_ai").SetValue(weapon, ai);
        hull.ShipSiegeWeapon = weapon;
        return (hull, ai);
    }
}
#endif
