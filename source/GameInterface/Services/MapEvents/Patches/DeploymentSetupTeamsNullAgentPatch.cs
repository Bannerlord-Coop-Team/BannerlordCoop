using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Null-guards the <c>InitialPlayerAgent</c> calls in native <see cref="DeploymentMissionController"/>.SetupTeams and
/// .FinishDeployment so a coop deployment with no local player hero on the field still completes. A hero downed in a
/// live coop battle that leaves and rejoins (no longer supplied, so it never respawns), or a wounded hero starting a
/// fresh battle (excluded from the flatten), leaves <c>Mission.InitialPlayerAgent</c> null; native SetupTeams then
/// dereferences it and throws every tick before reaching its <c>CanPlayerSideDeployWithOrderOfBattle</c> gate, wedging
/// the player in deployment forever (issue #3809). Skipping the calls lets SetupTeams reach that gate, which
/// <see cref="CoopAllowPlayerDeploymentPatch"/> forces off so deployment auto-finishes into the live battle through
/// FinishDeployment, which dereferences the same null agent and is guarded here too.
/// </summary>
[HarmonyPatch]
internal class DeploymentSetupTeamsNullAgentPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<DeploymentSetupTeamsNullAgentPatch>();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(DeploymentMissionController), "SetupTeams");
        yield return AccessTools.Method(typeof(DeploymentMissionController), nameof(DeploymentMissionController.FinishDeployment));
    }

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var redirects = new Dictionary<MethodInfo, MethodInfo>
        {
            [AccessTools.PropertySetter(typeof(Agent), nameof(Agent.Controller))] =
                AccessTools.Method(typeof(DeploymentSetupTeamsNullAgentPatch), nameof(SetControllerIfPresent)),
            [AccessTools.Method(typeof(Agent), nameof(Agent.SetIsAIPaused))] =
                AccessTools.Method(typeof(DeploymentSetupTeamsNullAgentPatch), nameof(SetIsAIPausedIfPresent)),
            [AccessTools.Method(typeof(Agent), nameof(Agent.SetDetachableFromFormation))] =
                AccessTools.Method(typeof(DeploymentSetupTeamsNullAgentPatch), nameof(SetDetachableFromFormationIfPresent)),
        };

        var replacements = 0;
        foreach (var code in instructions)
        {
            if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt)
                && code.operand is MethodInfo method && redirects.TryGetValue(method, out var replacement))
            {
                code.opcode = OpCodes.Call;
                code.operand = replacement;
                replacements++;
            }
            yield return code;
        }

        if (replacements == 0)
            Logger.Warning("Null-guarded no InitialPlayerAgent calls in {Method}; a game update may have changed the method",
                original?.Name);
    }

    // The calls native SetupTeams and FinishDeployment make on InitialPlayerAgent, each a no-op when the local
    // player has no hero agent on the field (leaderless coop deployment) rather than an NRE.
    private static void SetControllerIfPresent(Agent agent, AgentControllerType controller)
    {
        if (agent != null) agent.Controller = controller;
    }

    private static void SetIsAIPausedIfPresent(Agent agent, bool isPaused) => agent?.SetIsAIPaused(isPaused);

    private static void SetDetachableFromFormationIfPresent(Agent agent, bool value) => agent?.SetDetachableFromFormation(value);
}
