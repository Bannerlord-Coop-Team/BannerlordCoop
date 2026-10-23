#if DEBUG
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions;
using NavalDLC.Missions.AI.Behaviors;
using NavalDLC.Missions.AI.TeamAI;
using NavalDLC.Missions.MissionLogics;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// R46 capture crash: vanilla's swap left the prize's old formation with swimmers and no ship, and NavalQuerySystem's average
/// ship position read that formation's ship unchecked. The patch makes each unchecked lookup skip a formation without a ship.
/// </summary>
[Collection("Mission.Current")]
public class NavalShiplessFormationPatchTests
{
    [Fact]
    public void TargetMethods_FindTheThreeUncheckedQueries()
    {
        var targets = NavalShiplessFormationPatch.TargetMethods().ToArray();

        Assert.Equal(2, targets.Count(method => method.DeclaringType == typeof(NavalQuerySystem)));
        Assert.Single(targets, method => method.DeclaringType == typeof(ShipOrder));
    }

    [Fact]
    public void Transpiler_BranchesToTheLoopConditionWhenTheFormationHasNoShip()
    {
        foreach (var method in NavalShiplessFormationPatch.TargetMethods())
        {
            var body = PatchProcessor.GetOriginalInstructions(method, out var generator);
            var patched = NavalShiplessFormationPatch.Transpiler(body, generator).ToList();

            for (int i = 0; i < patched.Count; i++)
            {
                if (!(patched[i].operand is MethodInfo called) || called.Name != nameof(NavalShipsLogic.GetShip)) continue;

                Assert.NotEqual(OpCodes.Pop, patched[i + 1].opcode);
                if (patched[i + 1].opcode != OpCodes.Brfalse) continue;

                var label = (Label)patched[i + 1].operand;
                var condition = patched.Single(code => code.labels.Contains(label));
                var next = patched[patched.IndexOf(condition) + 1];
                Assert.Equal("MoveNext", ((MethodInfo)next.operand).Name);
            }
        }
    }

    [Fact]
    public void Transpiler_WithoutAnUncheckedLookup_FailsClosed()
    {
        Assert.Throws<InvalidOperationException>(() =>
            NavalShiplessFormationPatch.Transpiler(Array.Empty<CodeInstruction>(), null).ToArray());
    }

    [Fact]
    public void Patch_BindsToTheInstalledNavalDlc()
    {
        var harmony = new Harmony("coop.tests.naval.shipless-formation");
        try
        {
            Assert.Equal(3, harmony.CreateClassProcessor(typeof(NavalShiplessFormationPatch)).Patch().Count);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // R48 review: the three naval behaviours whose weight reads the formation's hull unchecked are all patched.
    [Fact]
    public void BehaviorWeightPatch_TargetsTheThreeUncheckedWeights()
    {
        var targets = ShiplessFormationBehaviorWeightPatch.TargetMethods().ToArray();

        Assert.Equal(3, targets.Length);
        Assert.All(targets, method => Assert.NotNull(method));
        Assert.Equal(new[] { "BehaviorNavalRamming", "BehaviorNavalRemoveConnection", "BehaviorNavalSkirmish" },
            targets.Select(method => method.DeclaringType!.Name).OrderBy(name => name).ToArray());
    }

    // Outside a co-op naval battle vanilla's weight runs.
    [Fact]
    public void BehaviorWeightPrefix_OutsideACoopBattle_RunsVanilla()
    {
        bool wasActive = CoopNavalMissionScope.IsActive;
        CoopNavalMissionScope.IsActive = false;
        try
        {
            float weight = 7f;
            Assert.True(ShiplessFormationBehaviorWeightPatch.Prefix(null!, ref weight));
            Assert.Equal(7f, weight);
        }
        finally
        {
            CoopNavalMissionScope.IsActive = wasActive;
        }
    }

    // R48 re-review: a behaviour whose cached hull is null (vanilla's refresh after a capture) weighs 0 in a co-op battle
    // instead of dereferencing it.
    [Fact]
    public void BehaviorWeightPrefix_InACoopBattle_WithNoCachedHull_WeighsNothing()
    {
        var behavior = (BehaviorNavalRemoveConnection)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
            typeof(BehaviorNavalRemoveConnection));
        bool wasActive = CoopNavalMissionScope.IsActive;
        CoopNavalMissionScope.IsActive = true;
        try
        {
            float weight = 5000f;
            Assert.False(ShiplessFormationBehaviorWeightPatch.Prefix(behavior, ref weight));
            Assert.Equal(0f, weight);
        }
        finally
        {
            CoopNavalMissionScope.IsActive = wasActive;
        }
    }

    // R48 re-review: what one weight evaluation does, by what is known about the hulls involved.
    [Theory]
    [InlineData(true, false, true, true, ShiplessFormationBehaviorWeightPatch.WeightCall.Weightless)]
    [InlineData(true, true, true, false, ShiplessFormationBehaviorWeightPatch.WeightCall.Weightless)]
    [InlineData(true, true, true, true, ShiplessFormationBehaviorWeightPatch.WeightCall.Vanilla)]
    [InlineData(true, true, false, true, ShiplessFormationBehaviorWeightPatch.WeightCall.ReReadThenVanilla)]
    [InlineData(false, false, false, true, ShiplessFormationBehaviorWeightPatch.WeightCall.Weightless)]
    [InlineData(false, false, true, true, ShiplessFormationBehaviorWeightPatch.WeightCall.Vanilla)]
    public void BehaviorWeight_Decide(bool known, bool formationHasShip, bool hasCachedShip, bool enemyHasShip,
        ShiplessFormationBehaviorWeightPatch.WeightCall expected)
    {
        Assert.Equal(expected, ShiplessFormationBehaviorWeightPatch.Decide(known, formationHasShip, hasCachedShip, enemyHasShip));
    }

    // A skirmish order at a missing target is not given in a co-op battle; outside one vanilla runs.
    [Fact]
    public void SkirmishOrderPrefix_SkipsAMissingTargetOnlyInACoopBattle()
    {
        bool wasActive = CoopNavalMissionScope.IsActive;
        try
        {
            CoopNavalMissionScope.IsActive = true;
            Assert.False(SkirmishOrderWithoutTargetPatch.Prefix(null!));
            CoopNavalMissionScope.IsActive = false;
            Assert.True(SkirmishOrderWithoutTargetPatch.Prefix(null!));
        }
        finally
        {
            CoopNavalMissionScope.IsActive = wasActive;
        }
    }

    // Ramming and skirmish skip their ship orders while their own cached hull is missing.
    [Fact]
    public void ShipOrdersPrefix_SkipsWithoutTheBehavioursOwnHull()
    {
        var ramming = (BehaviorNavalRamming)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
            typeof(BehaviorNavalRamming));
        bool wasActive = CoopNavalMissionScope.IsActive;
        CoopNavalMissionScope.IsActive = true;
        try
        {
            Assert.False(ShiplessTargetShipOrdersPatch.Prefix(ramming));
            Assert.Equal(2, ShiplessTargetShipOrdersPatch.TargetMethods().Count(method => method != null));
        }
        finally
        {
            CoopNavalMissionScope.IsActive = wasActive;
        }
    }

    // R48 round 4 review: engage and follow orders at a missing hull are not given in a co-op battle; outside one vanilla runs.
    [Fact]
    public void EngageAndFollowOrderPrefixes_SkipAMissingTargetOnlyInACoopBattle()
    {
        bool wasActive = CoopNavalMissionScope.IsActive;
        try
        {
            CoopNavalMissionScope.IsActive = true;
            Assert.False(EngageOrderWithoutTargetPatch.Prefix(null!));
            Assert.False(FollowOrderWithoutTargetPatch.Prefix(null!));
            CoopNavalMissionScope.IsActive = false;
            Assert.True(EngageOrderWithoutTargetPatch.Prefix(null!));
            Assert.True(FollowOrderWithoutTargetPatch.Prefix(null!));
        }
        finally
        {
            CoopNavalMissionScope.IsActive = wasActive;
        }
    }

    // The order setters run on the selected formations with a hull, and the full selection comes back in its order.
    [Fact]
    public void SelectedFormationsWithoutAHull_AreTakenOutForTheOrder_AndPutBackInPlace()
    {
        var formations = Enumerable.Range(0, 4)
            .Select(_ => (TaleWorlds.MountAndBlade.Formation)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                typeof(TaleWorlds.MountAndBlade.Formation)))
            .ToArray();
        var selected = formations.ToList();
        var shipless = new[] { formations[0], formations[2] };

        var taken = ShiplessSelectedFormationOrdersPatch.TakeShipless(selected, formation => !shipless.Contains(formation));
        Assert.Equal(new[] { formations[1], formations[3] }, selected);

        ShiplessSelectedFormationOrdersPatch.Restore(selected, taken);
        Assert.Equal(formations, selected);
        Assert.Equal(6, ShiplessSelectedFormationOrdersPatch.TargetMethods().Count(method => method != null));
    }

    // R48 round 5 review: a selection that is all hull-less (a swimming captured player selecting all) is emptied for the order,
    // so the setter is skipped, as several read SelectedFormations[0]; the restore still puts every formation back.
    [Fact]
    public void AllSelectedFormationsWithoutAHull_SkipTheSetter_AndAreRestored()
    {
        var formations = Enumerable.Range(0, 2)
            .Select(_ => (TaleWorlds.MountAndBlade.Formation)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                typeof(TaleWorlds.MountAndBlade.Formation)))
            .ToArray();
        var selected = formations.ToList();

        var taken = ShiplessSelectedFormationOrdersPatch.TakeShipless(selected, _ => false);

        Assert.Empty(selected);
        Assert.False(ShiplessSelectedFormationOrdersPatch.RunsOrder(selected.Count, taken.Count));
        ShiplessSelectedFormationOrdersPatch.Restore(selected, taken);
        Assert.Equal(formations, selected);
    }

    [Theory]
    [InlineData(0, 2, false)]
    [InlineData(1, 1, true)]
    [InlineData(2, 0, true)]
    [InlineData(0, 0, true)]
    public void RunsOrder_UnlessTakingTheHullLessOnesEmptiedTheSelection(int selectedLeft, int taken, bool expected)
    {
        Assert.Equal(expected, ShiplessSelectedFormationOrdersPatch.RunsOrder(selectedLeft, taken));
    }

    // A cancelled ramming on a hull without an AI controller stops, keeping its ignored target as vanilla does.
    [Fact]
    public void RammingCancel_WithoutAnAiController_StopsAndKeepsTheIgnoredTarget()
    {
        var ramming = (BehaviorNavalRamming)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
            typeof(BehaviorNavalRamming));
        var target = (NavalDLC.Missions.Objects.MissionShip)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
            typeof(NavalDLC.Missions.Objects.MissionShip));
        ramming._ignoredShip = target;
        ramming._isRammingActive = true;
        bool wasActive = CoopNavalMissionScope.IsActive;
        CoopNavalMissionScope.IsActive = true;
        try
        {
            Assert.False(RammingCancelWithoutAiControllerPatch.Prefix(ramming));
            Assert.False(ramming._isRammingActive);
            Assert.Same(target, ramming._ignoredShip);
        }
        finally
        {
            CoopNavalMissionScope.IsActive = wasActive;
        }
    }
}
#endif
