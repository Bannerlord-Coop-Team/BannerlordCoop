using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>Verifies that the BattleAgentLogic null-map-event guards are discoverable by Harmony.</summary>
public class BattleAgentLogicPatchTests
{
    [Fact]
    public void BattleAgentLogicNullMapEventPatches_IsDiscoverableByPatchAll_AndHooksTargets()
    {
        var patchType = typeof(BattleSpawnGate).Assembly
            .GetType("GameInterface.Services.MapEvents.Patches.BattleAgentLogicNullMapEventPatches");
        Assert.NotNull(patchType);
        Assert.Contains(patchType.GetCustomAttributes(inherit: true), attribute => attribute is HarmonyPatch);

        var harmony = new Harmony("e2e.battleagentlogic.patchtest");
        try
        {
            var patched = harmony.CreateClassProcessor(patchType).Patch()
                          ?? new List<MethodInfo>();
            var patchedMethodNames = patched.Select(method => method.Name).ToArray();

            Assert.Contains(patchedMethodNames, name => name.Contains("OnScoreHit"));
            Assert.Contains(patchedMethodNames, name => name.Contains("OnAgentBuild"));
            Assert.Contains(patchedMethodNames, name => name.Contains("CheckUpgrade"));
        }
        finally
        {
            harmony.UnpatchAll("e2e.battleagentlogic.patchtest");
        }
    }
}
