using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Extentions;
using GameInterface.Policies;
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
    /// Disables functionality of policies in game.
    /// </summary>
    /// <seealso cref="PolicyObject"/>
    [HarmonyPatch(typeof(Kingdom))]
    internal class KingdomPatches
    {
        [HarmonyPatch(nameof(Kingdom.AddDecision))]
        [HarmonyPrefix]
        public static bool AddDecisionPrefix(Kingdom __instance, KingdomDecision kingdomDecision, bool ignoreInfluenceCost)
        {
            if (AllowedThread.IsThisThreadAllowed())
            {
                ModifiedAddDecision(__instance, kingdomDecision, ignoreInfluenceCost);
                return false;
            }
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient) return false;

            ModifiedAddDecision(__instance, kingdomDecision, ignoreInfluenceCost);
            MessageBroker.Instance.Publish(__instance,
                new DecisionAdded(__instance.StringId, kingdomDecision.ToKingdomDecisionData(), ignoreInfluenceCost));
            return false;
        }

        public static void RunCoopAddDecision(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost, float randomFloat)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                ModifiedAddDecision(kingdom, kingdomDecision, ignoreInfluenceCost, randomFloat);
            }, true); 
        }

        private static void ModifiedAddDecision(Kingdom __instance, KingdomDecision kingdomDecision, bool ignoreInfluenceCost, float? randomFloat = null)
        {
            if (!ignoreInfluenceCost)
            {
                Clan proposerClan = kingdomDecision.ProposerClan;
                int influenceCost = kingdomDecision.GetInfluenceCost(proposerClan);
                ChangeClanInfluenceAction.Apply(proposerClan, (float)(-(float)influenceCost));
            }
            bool flag;
            if (!kingdomDecision.DetermineChooser().Leader.IsHumanPlayerCharacter)
            {
                flag = kingdomDecision.DetermineSupporters().Any((Supporter x) => x.IsPlayer);
            }
            else
            {
                flag = true;
            }

            bool isPlayerInvolved = flag;
            CampaignEventDispatcher.Instance.OnKingdomDecisionAdded(kingdomDecision, isPlayerInvolved);

            var playerParties = Campaign.Current.CampaignObjectManager.GetPlayerMobileParties();
            if (playerParties.All(party => party.ActualClan.Kingdom != kingdomDecision.Kingdom))
            {
                new CoopKingdomElection(kingdomDecision, randomFloat).StartElectionCoop();
                return;
            }

            __instance._unresolvedDecisions.Add(kingdomDecision);
        }

        [HarmonyPatch(nameof(Kingdom.RemoveDecision))]
        [HarmonyPrefix]
        public static bool RemoveDecisionPrefix(Kingdom __instance, KingdomDecision kingdomDecision)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient) return false;

            var index = __instance._unresolvedDecisions.FindIndex(decision => decision == kingdomDecision);

            MessageBroker.Instance.Publish(__instance,
                new DecisionRemoved(__instance.StringId, index));

            return true;
        }

        public static void RunOriginalRemoveDecision(Kingdom kingdom, KingdomDecision kingdomDecision)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    kingdom.RemoveDecision(kingdomDecision);
                }
            }, true);
        }

        [HarmonyPatch("AddPolicy")]
        [HarmonyPrefix]
        public static bool AddPolicyPrefix()
        {
            return false;
        }

        [HarmonyPatch("RemovePolicy")]
        [HarmonyPrefix]
        public static bool RemovePolicyPrefix()
        {
            return false;
        }
    }
}
