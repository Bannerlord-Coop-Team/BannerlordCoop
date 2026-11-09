using System.Linq;
using System.Reflection;
using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class CoopEmptyTeamDeploymentPatchTests
{
    private static readonly MethodInfo IsPlanMade = AccessTools.Method(typeof(DefaultMissionDeploymentPlan),
        nameof(DefaultMissionDeploymentPlan.IsPlanMade), new[] { typeof(Team) });
    private static readonly MethodInfo SpawnGate = AccessTools.Method(typeof(CoopEmptyTeamDeploymentPatch),
        nameof(CoopEmptyTeamDeploymentPatch.IsPlanMadeForSpawnGate));

    // An inlined IsPlanMade skips its postfix, so CheckDeployment must call the override directly (#4223)
    [Fact]
    public void CheckDeployment_ReadsEveryTeamPlanThroughTheSpawnGate()
    {
        var checkDeployment = AccessTools.Method(typeof(DefaultBattleMissionAgentSpawnLogic), "CheckDeployment");
        var original = PatchProcessor.GetOriginalInstructions(checkDeployment);
        int originalCalls = original.Count(instruction => instruction.Calls(IsPlanMade));
        Assert.True(originalCalls > 0);

        var patched = CoopEmptyTeamDeploymentPatch.CheckDeployment_Transpiler(original).ToList();

        Assert.DoesNotContain(patched, instruction => instruction.Calls(IsPlanMade));
        Assert.Equal(originalCalls, patched.Count(instruction => instruction.Calls(SpawnGate)));
        Assert.Equal(original.Count, patched.Count);
    }
}
