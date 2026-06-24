using GameInterface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
namespace GameInterface.Services.Kingdoms.Patches
{
    /// <summary>
    /// Routes <see cref="Kingdom"/> decision and policy mutations so the authoritative
    /// server replicates them to every client.
    /// </summary>
    /// <seealso cref="PolicyObject"/>
    [HarmonyPatch(typeof(Kingdom))]
    internal class KingdomPatches
    {
        [HarmonyPatch(nameof(Kingdom.AddDecision))]
        [HarmonyPrefix]
        public static bool AddDecisionPrefix(Kingdom __instance, KingdomDecision kingdomDecision, bool ignoreInfluenceCost)
        {
            if (!TryGetKingdomInterface(out var kingdomInterface)) return true;
            return kingdomInterface.AddDecisionPrefix(__instance, kingdomDecision, ignoreInfluenceCost);
        }
        [HarmonyPatch(nameof(Kingdom.RemoveDecision))]
        [HarmonyPrefix]
        public static bool RemoveDecisionPrefix(Kingdom __instance, KingdomDecision kingdomDecision)
        {
            if (!TryGetKingdomInterface(out var kingdomInterface)) return true;
            return kingdomInterface.RemoveDecisionPrefix(__instance, kingdomDecision);
        }
        [HarmonyPatch("AddPolicy")]
        [HarmonyPrefix]
        public static bool AddPolicyPrefix(Kingdom __instance, PolicyObject policy)
        {
            if (!TryGetKingdomInterface(out var kingdomInterface)) return true;
            return kingdomInterface.AddPolicyPrefix(__instance, policy);
        }
        [HarmonyPatch("RemovePolicy")]
        [HarmonyPrefix]
        public static bool RemovePolicyPrefix(Kingdom __instance, PolicyObject policy)
        {
            if (!TryGetKingdomInterface(out var kingdomInterface)) return true;
            return kingdomInterface.RemovePolicyPrefix(__instance, policy);
        }
        private static bool TryGetKingdomInterface(out IKingdomInterface kingdomInterface)
        {
            return ContainerProvider.TryResolve(out kingdomInterface);
        }
    }
}
