using Common.Messaging;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Patches
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

    [HarmonyPatch(typeof(Agent))]
    internal class HandleBlowAuxPatch
    {
        [HarmonyPatch("HandleBlowAux")]
        private static bool Prefix(ref  Agent __instance)
        {
            return __instance.Health < 1f == false;
        }
    }
}
