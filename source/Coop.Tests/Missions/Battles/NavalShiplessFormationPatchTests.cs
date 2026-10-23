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
}
#endif
