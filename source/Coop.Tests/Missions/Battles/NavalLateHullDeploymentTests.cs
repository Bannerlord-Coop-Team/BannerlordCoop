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
}
#endif
