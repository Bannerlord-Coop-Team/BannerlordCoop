using Common;
using Common.Messaging;
using GameInterface;
using GameInterface.Policies;
using GameInterface.Services.Kingdoms.Messages;
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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            if (!TryGetKingdomInterface(out var kingdomInterface)) return true;
            if (ModInformation.IsClient)
            {
                float clientRandomNumber = kingdomInterface.AddDecision(__instance, kingdomDecision, ignoreInfluenceCost, applyInfluenceCost: false);
                MessageBroker.Instance.Publish(__instance,
                    new DecisionAdded(__instance, kingdomDecision, ignoreInfluenceCost, clientRandomNumber));
                return false;
            }
            float randomNumber = kingdomInterface.AddDecision(__instance, kingdomDecision, ignoreInfluenceCost, applyInfluenceCost: true);
            MessageBroker.Instance.Publish(__instance,
                new DecisionAdded(__instance, kingdomDecision, ignoreInfluenceCost, randomNumber));
            return false;
        }
        [HarmonyPatch(nameof(Kingdom.RemoveDecision))]
        [HarmonyPrefix]
        public static bool RemoveDecisionPrefix(Kingdom __instance, KingdomDecision kingdomDecision)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            if (ModInformation.IsClient) return false;
            KingdomRegistry.EnsureRuntimeCollections(__instance);
            var index = __instance._unresolvedDecisions?.FindIndex(decision => decision == kingdomDecision) ?? -1;
            if (index >= 0)
            {
                MessageBroker.Instance.Publish(__instance,
                    new DecisionRemoved(__instance, index));
            }
            return true;
        }
        [HarmonyPatch("AddPolicy")]
        [HarmonyPrefix]
        public static bool AddPolicyPrefix(Kingdom __instance, PolicyObject policy)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            if (ModInformation.IsClient) return false;
            KingdomRegistry.EnsureRuntimeCollections(__instance);
            // Vanilla AddPolicy is an idempotent no-op when the policy is already active; only
            // announce a change that will actually take effect.
            if (!__instance._activePolicies.Contains(policy))
            {
                MessageBroker.Instance.Publish(__instance, new KingdomPolicyChanged(__instance, policy, isAdd: true));
            }
            return true;
        }
        [HarmonyPatch("RemovePolicy")]
        [HarmonyPrefix]
        public static bool RemovePolicyPrefix(Kingdom __instance, PolicyObject policy)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            if (ModInformation.IsClient) return false;
            KingdomRegistry.EnsureRuntimeCollections(__instance);
            // Vanilla RemovePolicy is an idempotent no-op when the policy is not active; only
            // announce a change that will actually take effect.
            if (__instance._activePolicies.Contains(policy))
            {
                MessageBroker.Instance.Publish(__instance, new KingdomPolicyChanged(__instance, policy, isAdd: false));
            }
            return true;
        }
        private static bool TryGetKingdomInterface(out IKingdomInterface kingdomInterface)
        {
            return ContainerProvider.TryResolve(out kingdomInterface);
        }
    }
}
