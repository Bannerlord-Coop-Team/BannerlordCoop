#if COOP_NAVAL
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Objects.Usables;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Late hulls get vanilla's deployment pass, which runs only on hulls spawned before deployment ends.</summary>
[Collection("Mission.Current")]
public class NavalLateHullDeploymentTests
{
    // Late hulls get vanilla's own deployment pass, and the ballistas fielded before them re-read their targets.
    [Fact]
    public void Target_VanillaDeploymentPassExists()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(MissionShip), nameof(MissionShip.OnDeploymentFinished)));
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(RangedSiegeWeaponAi), nameof(RangedSiegeWeaponAi.ResetThreatSeeker)));
    }

    [Fact]
    public void Deploy_GivesLateHullClimbingPointsTheirOwnMovementAndReleaseCleanup()
    {
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        var point = new StandingPointWithSwimmingLimit();
        var net = new ClimbingMachine();
        AccessTools.PropertySetter(typeof(UsableMachine), nameof(UsableMachine.StandingPoints))
            .Invoke(net, new object[] { new MBList<StandingPoint> { point } });
        hull._attachmentMachines = new MBList<ShipAttachmentMachine>();
        hull._attachmentPointMachines = new MBList<ShipAttachmentPointMachine>();
        hull._leftSideShipOarMachines = new MBList<ShipOarMachine>();
        hull._rightSideShipOarMachines = new MBList<ShipOarMachine>();
        hull._climbingMachines = new MBList<ClimbingMachine> { net };
        hull.ShipControllerMachine = ShipTestHulls.Uninitialized<HelmWithoutMission>();
        var deployment = new NavalLateHullDeployment(new NavalBallistaTargets());

        var harmony = new Harmony("coop.tests.naval.late-hull-climbing");
        Exception? failure;
        try
        {
            harmony.Patch(AccessTools.Method(typeof(MBAnimation), nameof(MBAnimation.GetActionCodeWithName)),
                prefix: new HarmonyMethod(typeof(NavalLateHullDeploymentTests), nameof(StubActionCode)));

            // The helm runs right after the climbing machines and stops the test before the live-mission setup.
            failure = Record.Exception(() => deployment.Deploy(hull));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }

        Assert.True(point.GetIsUserPositionsUpdatedInTheMachineTick());
        Assert.NotNull(point.GetComponent<ResetGravityExclusionAndEntityAttachmentOnStopUsageComponent>());
        Assert.NotNull(point.GetComponent<ResetAnimationOnStopUsageComponent>());
        Assert.IsType<HelmReachedException>(failure);
    }

    private static bool StubActionCode(string __0, ref int __result)
    {
        // Match MangonelLoadReplicationTests when this test initializes the shared action cache first.
        __result = __0 == "act_pickup_boulder_begin" ? 21 : __0 == "act_pickup_boulder_end" ? 22 : 0;
        return false;
    }

    // A formation noted twice is checked once; one without a formation AI is left alone, and the next tick starts empty.
    [Fact]
    public void Apply_ChecksEachNotedFormationOnce_ThenForgetsThem()
    {
        var deployment = new NavalLateHullDeployment(new NavalBallistaTargets());
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

    /// <summary>Stops deployment after the real climbing net setup and before native helm and hull work.</summary>
    private sealed class HelmWithoutMission : ShipControllerMachine
    {
        public override void OnDeploymentFinished() => throw new HelmReachedException();
    }

    /// <summary>Signals that deployment reached the helm.</summary>
    private sealed class HelmReachedException : Exception { }
}
#endif
