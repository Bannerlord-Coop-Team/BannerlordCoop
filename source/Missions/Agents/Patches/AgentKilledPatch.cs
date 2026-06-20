using Common.Messaging;
using HarmonyLib;
using Missions.Agents.Messages;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches
{
    /// <summary>
    /// Patch on OnHealthChanged to publish Agent deaths
    /// </summary>
    [HarmonyPatch(typeof(Agent))]
    internal class AgentKilledPatch
    {
        [HarmonyPatch(nameof(Agent.Die))]
        [HarmonyPostfix]
        private static void OnDeath(ref Agent __instance)
        {
            if(__instance.Health <= 0)
            {
                MessageBroker.Instance.Publish(__instance, new AgentDied(__instance));
            }
        }
    }
}
