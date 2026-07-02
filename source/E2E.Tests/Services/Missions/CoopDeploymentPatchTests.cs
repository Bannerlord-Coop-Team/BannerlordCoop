using System.Linq;
using System.Reflection;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Guards the Harmony WIRING of the coop deployment patches — not their behaviour (the spawn gate is native and
/// live-only), but the fact that they are DISCOVERABLE and actually hook their target methods. A multi-method patch
/// class that targets more than one declaring type must carry a bare class-level <c>[HarmonyPatch]</c>; without it
/// <c>Harmony.PatchAll</c> silently skips the whole class and the patch never installs. That exact mistake on
/// <c>CoopEmptyTeamDeploymentPatch</c> stopped the non-host from spawning its own troops (the empty/foreign ally
/// team stalled <c>DefaultBattleMissionAgentSpawnLogic.CheckDeployment</c> with no override to clear it).
/// </summary>
public class CoopDeploymentPatchTests
{
    public CoopDeploymentPatchTests(ITestOutputHelper output) { }

    [Fact]
    public void CoopEmptyTeamDeploymentPatch_IsDiscoverableByPatchAll_AndHooksItsTargets()
    {
        // The patch class is internal to GameInterface; reach it via the assembly so the test sees what PatchAll sees.
        var patchType = typeof(BattleSpawnGate).Assembly
            .GetType("GameInterface.Services.MapEvents.Patches.CoopEmptyTeamDeploymentPatch");
        Assert.NotNull(patchType);

        var harmony = new Harmony("e2e.coopdeploy.patchtest");
        try
        {
            // CreateClassProcessor(...).Patch() is exactly what PatchAll runs per type. With no class-level
            // [HarmonyPatch] it returns NOTHING (the bug); with it, it hooks the two target methods below.
            var patched = harmony.CreateClassProcessor(patchType).Patch()
                          ?? new System.Collections.Generic.List<MethodInfo>();

            // Patch() returns the hooked methods (Harmony renames them <Name>_PatchN), so match by name, not by
            // MethodInfo identity. Empty/missing entries = the class wasn't processed = the patch never installs.
            Assert.Contains(patched, m => m.Name.Contains("IsPlanMade"));
            Assert.Contains(patched, m => m.Name.Contains("MakeTeamPlans"));
        }
        finally
        {
            harmony.UnpatchAll("e2e.coopdeploy.patchtest");
        }
    }
}
