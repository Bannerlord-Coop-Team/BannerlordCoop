using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using System.Reflection;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using Common.Extensions;
using GameInterface.Policies;
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Extentions;

namespace GameInterface.Services.Kingdoms.Patches
{
    /// <summary>
    /// Disables functionality of policies in game.
    /// </summary>
    /// <seealso cref="PolicyObject"/>
    [HarmonyPatch(typeof(Kingdom))]
    internal class KingdomPatches
    {

        private static Func<Kingdom, MBList<KingdomDecision>> GetUnresolvedDecisions = typeof(Kingdom).GetField("_unresolvedDecisions", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<Kingdom, MBList<KingdomDecision>>();
        private static readonly AllowedInstance<Kingdom> AllowedInstance = new AllowedInstance<Kingdom>();

        [HarmonyPatch(nameof(Kingdom.AddDecision))]
        [HarmonyPrefix]
        public static bool AddDecisionPrefix(Kingdom __instance, KingdomDecision kingdomDecision, bool ignoreInfluenceCost)
        {
            if (AllowedThread.IsThisThreadAllowed())
            {
                ModifiedAddDecision(__instance, kingdomDecision, ignoreInfluenceCost);
                return false;
            }
            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient) return false;

            ModifiedAddDecision(__instance, kingdomDecision, ignoreInfluenceCost);
            MessageBroker.Instance.Publish(__instance,
                new LocalDecisionAdded(__instance.StringId, kingdomDecision.ToKingdomDecisionData(), ignoreInfluenceCost));
            return false;
        }

        public static void RunOriginalAddDecision(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    kingdom.AddDecision(kingdomDecision, ignoreInfluenceCost);
                }
            }, true); 
        }

        private static void ModifiedAddDecision(Kingdom __instance, KingdomDecision kingdomDecision, bool ignoreInfluenceCost)
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
                new KingdomElection(kingdomDecision).StartElection();
                return;
            }

            GetUnresolvedDecisions(__instance).Add(kingdomDecision);
        }

        [HarmonyPatch(nameof(Kingdom.RemoveDecision))]
        [HarmonyPrefix]
        public static bool RemoveDecisionPrefix(Kingdom __instance, KingdomDecision kingdomDecision)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (PolicyProvider.AllowOriginalCalls) return true;

            if (ModInformation.IsClient) return false;

            var index = GetUnresolvedDecisions(__instance).FindIndex(decision => decision == kingdomDecision);

            MessageBroker.Instance.Publish(__instance,
                new LocalDecisionRemoved(__instance.StringId, index));

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
