using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches
{
    /// <summary>
    /// Prevents NullReferenceException in <see cref="HumanAIComponent.FindClosestMountAvailable"/>
    /// when checking candidate mount agents that have a null <see cref="Agent.CommonAIComponent"/>.
    /// </summary>
    [HarmonyPatchCategory(MissionModule.MountPatchCategory)]
    [HarmonyPatch(typeof(HumanAIComponent), nameof(HumanAIComponent.FindClosestMountAvailable))]
    internal class FindClosestMountAvailablePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(HumanAIComponent __instance, ref Agent __result)
        {
            __result = null;

            if (__instance?.Agent == null || Mission.Current?.Agents == null)
            {
                return false;
            }

            float minDistanceSq = 100000000f;
            Agent closestMount = null;

            foreach (Agent candidate in Mission.Current.Agents)
            {
                if (candidate != null &&
                    candidate.IsMount &&
                    candidate.RiderAgent == null &&
                    candidate.IsAIControlled &&
                    candidate.CommonAIComponent != null &&
                    candidate.CommonAIComponent.ReservedRiderAgentIndex >= 0)
                {
                    float distSq = __instance.Agent.Position.DistanceSquared(candidate.Position);
                    if (distSq < minDistanceSq)
                    {
                        minDistanceSq = distSq;
                        closestMount = candidate;
                    }
                }
            }

            __result = closestMount;
            return false;
        }
    }
}
