#if DEBUG
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions;
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
}
#endif
