using HarmonyLib;
using Missions.Agents.Extensions;
using NavalDLC.Missions;

namespace Missions.Naval;

/// <summary>
/// Only an agent's owner drowns it. Vanilla queues a 10 damage self-blow every 5 s for an agent under water, and a puppet's
/// local pose can sit under water here, so that blow would be routed to the owner as a second drowning.
/// </summary>
[HarmonyPatch(typeof(AgentNavalComponent), nameof(AgentNavalComponent.DrownAgent))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalPuppetDrowningPatch
{
    // Skips vanilla's drowning blow on an agent another client owns.
    [HarmonyPrefix]
    private static bool Prefix(AgentNavalComponent __instance) => !__instance.Agent.IsPuppet();
}
