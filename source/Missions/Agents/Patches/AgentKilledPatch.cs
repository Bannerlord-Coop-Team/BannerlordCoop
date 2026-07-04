using Common.Logging;
using Common.Messaging;
using HarmonyLib;
using Missions.Agents.Messages;
using Serilog;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches
{
    /// <summary>
    /// Patch on OnHealthChanged to publish Agent deaths
    /// </summary>
    [HarmonyPatch(typeof(Agent))]
    internal class AgentKilledPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<AgentKilledPatch>();

        [HarmonyPatch(nameof(Agent.Die))]
        [HarmonyPostfix]
        private static void OnDeath(ref Agent __instance)
        {
            // Temporary #1704/#1705 diagnostic: confirms whether Missions.dll's Harmony patches on
            // vanilla types are actually applied at all. Remove once ScoreboardSideDiagnosticPatch is resolved.
            Logger.Information("[SideDiag] AgentKilledPatch.OnDeath fired for agent {Agent}", __instance);
            if(__instance.Health <= 0)
            {
                MessageBroker.Instance.Publish(__instance, new AgentDied(__instance));
            }
        }
    }
}
