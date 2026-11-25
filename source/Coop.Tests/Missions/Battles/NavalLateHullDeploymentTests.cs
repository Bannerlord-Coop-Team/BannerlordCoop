#if COOP_NAVAL
using Missions.Naval;
using NavalDLC.Missions.Objects;
using System;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Late-hull formations are checked once per tick, and only weightless formations with units ask the tactic to re-apply.</summary>
public class NavalLateHullDeploymentTests
{
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
