using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Patches
{
    [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.RemovePolicy))]
    public class KingdomRemovePolicyPatch
    {
        private static AllowedInstance<Kingdom> _allowedInstance;

        public static bool Prefix(Kingdom __instance, PolicyObject policy)
        {
            if (__instance == _allowedInstance?.Instance) return true;

            MessageBroker.Instance.Publish(__instance, new RemovePolicy(policy.StringId, __instance.StringId));

            return false;
        }

        public static void RunOriginalRemovePolicy(PolicyObject policy, Kingdom kingdom)
        {
            using (_allowedInstance = new AllowedInstance<Kingdom>(kingdom))
            {
                GameLoopRunner.RunOnMainThread(() =>
                {
                    kingdom.RemovePolicy(policy);
                }, true);
            }
        }
    }
}