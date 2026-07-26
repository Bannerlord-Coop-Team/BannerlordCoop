using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches;

/// <summary>
/// Avoids the native null dereference when an active mount has no CommonAIComponent.
/// </summary>
[HarmonyPatchCategory(MissionModule.MountPatchCategory)]
[HarmonyPatch(typeof(HumanAIComponent), nameof(HumanAIComponent.FindClosestMountAvailable))]
internal class FindClosestMountAvailablePatch
{
    private const float MaximumDistanceSquared = 6400f;

    [HarmonyPrefix]
    private static bool Prefix(HumanAIComponent __instance, ref Agent __result)
    {
        Mission mission = Mission.Current;
        if (__instance?.Agent == null || mission == null)
        {
            __result = null;
            return false;
        }

        __result = FindClosestMountAvailableSafe(__instance, mission);
        return false;
    }

    private static Agent FindClosestMountAvailableSafe(
        HumanAIComponent humanAi,
        Mission mission)
    {
        float closestUnreservedDistanceSquared = MaximumDistanceSquared;
        Agent closestUnreserved = null;
        float closestDistanceSquared = MaximumDistanceSquared;
        Agent closest = null;

        foreach (KeyValuePair<Agent, MissionTime> entry in mission.MountsWithoutRiders)
        {
            Agent candidate = entry.Key;
            if (candidate == null ||
                !candidate.IsActive() ||
                candidate.RiderAgent != null ||
                candidate.IsRunningAway ||
                candidate.CommonAIComponent == null ||
                !MissionGameModels.Current.AgentStatCalculateModel.CanAgentRideMount(
                    humanAi.Agent,
                    candidate))
            {
                continue;
            }

            float distanceSquared =
                humanAi.Agent.Position.DistanceSquared(candidate.Position);
            if (distanceSquared < closestDistanceSquared)
            {
                closest = candidate;
                closestDistanceSquared = distanceSquared;
            }

            if (candidate.CommonAIComponent.ReservedRiderAgentIndex < 0 &&
                distanceSquared < closestUnreservedDistanceSquared)
            {
                closestUnreserved = candidate;
                closestUnreservedDistanceSquared = distanceSquared;
            }
        }

        if (closest == closestUnreserved)
            return closest;

        if (closestUnreserved != null &&
            closestUnreservedDistanceSquared > 0.01f &&
            closestDistanceSquared / closestUnreservedDistanceSquared >= 0.4f)
        {
            return closestUnreserved;
        }

        int reservedRiderIndex =
            closest.CommonAIComponent.ReservedRiderAgentIndex;
        Agent reservedRider = mission.FindAgentWithIndex(reservedRiderIndex);
        if (reservedRider?.HumanAIComponent == null)
            return closestUnreserved;

        float reservedRiderDistanceSquared =
            reservedRider.Position.DistanceSquared(closest.Position);
        if (reservedRiderDistanceSquared > 0.01f &&
            closestDistanceSquared / reservedRiderDistanceSquared <
            (closestUnreserved != null ? 0.4f : 0.7f))
        {
            reservedRider.HumanAIComponent.UnreserveMount(closest);
            return closest;
        }

        return closestUnreserved;
    }
}
