using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Extentions;
using GameInterface.Policies;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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

            if (ModInformation.IsClient)
            {
                float clientRandomNumber = ModifiedAddDecision(__instance, kingdomDecision, ignoreInfluenceCost);
                MessageBroker.Instance.Publish(__instance,
                    new DecisionAdded(__instance, kingdomDecision.ToKingdomDecisionData(), ignoreInfluenceCost, clientRandomNumber));
                return false;
            }

            float randomNumber = ModifiedAddDecision(__instance, kingdomDecision, ignoreInfluenceCost);
            MessageBroker.Instance.Publish(__instance,
                new DecisionAdded(__instance, kingdomDecision.ToKingdomDecisionData(), ignoreInfluenceCost, randomNumber));
            return false;
        }

        public static void RunCoopAddDecision(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost, float randomFloat)
        {
            GameThread.Run(() =>
            {
                using (new AllowedThread())
                {
                    ModifiedAddDecision(kingdom, kingdomDecision, ignoreInfluenceCost, randomFloat);
                }
            }, true); 
        }

        private static float ModifiedAddDecision(Kingdom __instance, KingdomDecision kingdomDecision, bool ignoreInfluenceCost, float? randomFloat = null)
        {
            KingdomRegistry.EnsureRuntimeCollections(__instance);

            if (!ignoreInfluenceCost)
            {
                Clan proposerClan = kingdomDecision.ProposerClan;
                int influenceCost = kingdomDecision.GetInfluenceCost(proposerClan);
                ChangeClanInfluenceAction.Apply(proposerClan, (float)(-(float)influenceCost));
            }
            bool isPlayerInvolved = IsCoopPlayerInvolved(kingdomDecision);
            CampaignEventDispatcher.Instance.OnKingdomDecisionAdded(kingdomDecision, isPlayerInvolved);

            var playerParties = Campaign.Current.CampaignObjectManager.GetPlayerMobileParties();
            if (playerParties.All(party => party.ActualClan.Kingdom != kingdomDecision.Kingdom))
            {
                CoopKingdomElection election = new CoopKingdomElection(kingdomDecision, randomFloat);
                election.StartElectionCoop();
                return election.RandomFloat;
            }

            __instance._unresolvedDecisions.Add(kingdomDecision);
            KingdomDecisionVoteManager.RegisterDecision(kingdomDecision);
            return default;
        }

        private static bool IsCoopPlayerInvolved(KingdomDecision kingdomDecision)
        {
            if (KingdomDecisionVoteManager.HasEligiblePlayerClan(kingdomDecision))
            {
                return true;
            }

            var playerParties = Campaign.Current?.CampaignObjectManager?.GetPlayerMobileParties();
            if (playerParties == null) return false;

            return playerParties.Any(party => party?.ActualClan?.Kingdom == kingdomDecision.Kingdom);
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

        public static void RunOriginalRemoveDecision(Kingdom kingdom, KingdomDecision kingdomDecision)
        {
            GameThread.Run(() =>
            {
                using (new AllowedThread())
                {
                    kingdom.RemoveDecision(kingdomDecision);
                }
            }, true);
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
            if (!__instance.ActivePolicies.Contains(policy))
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
            if (__instance.ActivePolicies.Contains(policy))
            {
                MessageBroker.Instance.Publish(__instance, new KingdomPolicyChanged(__instance, policy, isAdd: false));
            }
            return true;
        }

        public static void RunChangeKingdomPolicy(Kingdom kingdom, PolicyObject policy, bool isAdd)
        {
            GameThread.Run(() =>
            {
                using (new AllowedThread())
                {
                    KingdomRegistry.EnsureRuntimeCollections(kingdom);

                    if (isAdd)
                    {
                        kingdom.AddPolicy(policy);
                    }
                    else
                    {
                        kingdom.RemovePolicy(policy);
                    }
                }
            }, true);
        }
    }
}
