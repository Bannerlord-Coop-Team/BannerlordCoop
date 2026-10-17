using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.GameInterface.Services.MapEvents;

/// <summary>
/// Verifies the transpiler for <see cref="DeploymentSetupTeamsNullAgentPatch"/> redirects the three InitialPlayerAgent
/// calls native SetupTeams makes to the null-safe wrappers, and leaves unrelated calls untouched (#3809).
/// </summary>
public class DeploymentSetupTeamsNullAgentPatchTests
{
    [Fact]
    public void Transpiler_RedirectsInitialPlayerAgentCalls_ToNullSafeWrappers()
    {
        var setController = AccessTools.PropertySetter(typeof(Agent), nameof(Agent.Controller));
        var setIsAIPaused = AccessTools.Method(typeof(Agent), nameof(Agent.SetIsAIPaused));
        var setDetachable = AccessTools.Method(typeof(Agent), nameof(Agent.SetDetachableFromFormation));
        var unrelated = AccessTools.Method(typeof(object), nameof(ToString));

        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Callvirt, setController),
            new CodeInstruction(OpCodes.Callvirt, setIsAIPaused),
            new CodeInstruction(OpCodes.Callvirt, setDetachable),
            new CodeInstruction(OpCodes.Callvirt, unrelated),
        };

        var output = DeploymentSetupTeamsNullAgentPatch.Transpiler(input, original: null).ToList();

        AssertRedirected(output[0], "SetControllerIfPresent");
        AssertRedirected(output[1], "SetIsAIPausedIfPresent");
        AssertRedirected(output[2], "SetDetachableFromFormationIfPresent");

        Assert.Equal(OpCodes.Callvirt, output[3].opcode);
        Assert.Same(unrelated, output[3].operand);
    }

    private static void AssertRedirected(CodeInstruction instruction, string wrapperName)
    {
        Assert.Equal(OpCodes.Call, instruction.opcode);
        var method = Assert.IsAssignableFrom<MethodInfo>(instruction.operand);
        Assert.Equal(typeof(DeploymentSetupTeamsNullAgentPatch), method.DeclaringType);
        Assert.Equal(wrapperName, method.Name);
    }
}
