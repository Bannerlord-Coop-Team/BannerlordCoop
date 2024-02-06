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
            if (AllowedInstance.IsAllowed(__instance)) return true;

            MessageBroker.Instance.Publish(__instance,
                new LocalDecisionAdded(__instance.StringId, kingdomDecision.ToKingdomDecisionData(), ignoreInfluenceCost));

            return false;
        }

        public static void RunOriginalAddDecision(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = kingdom;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    kingdom.AddDecision(kingdomDecision, ignoreInfluenceCost);
                }, true);
            }
        }

        [HarmonyPatch(nameof(Kingdom.RemoveDecision))]
        [HarmonyPrefix]
        public static bool RemoveDecisionPrefix(Kingdom __instance, KingdomDecision kingdomDecision)
        {
            if (AllowedInstance.IsAllowed(__instance)) return true;

            var index = GetUnresolvedDecisions(__instance).FindIndex(decision => decision == kingdomDecision);

            MessageBroker.Instance.Publish(__instance,
                new LocalDecisionRemoved(__instance.StringId, index));

            return false;
        }

        public static void RunOriginalRemoveDecision(Kingdom kingdom, KingdomDecision kingdomDecision)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = kingdom;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    kingdom.RemoveDecision(kingdomDecision);
                }, true);
            }
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
