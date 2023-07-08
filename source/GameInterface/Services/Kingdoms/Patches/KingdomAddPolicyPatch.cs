using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction;

namespace GameInterface.Services.Kingdoms.Patches
{
    [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.AddPolicy))]
    public class KingdomAddPolicyPatch
    {
        private static AllowedInstance<Kingdom> _allowedInstance;

        public static bool Prefix(Kingdom __instance, PolicyObject policy)
        {
            if (__instance == _allowedInstance?.Instance) return true;

            MessageBroker.Instance.Publish(__instance, new AddPolicy(policy.StringId, __instance.StringId));

            return false;
        }

        public static void RunOriginalAddPolicy(PolicyObject policy, Kingdom kingdom)
        {
            using (_allowedInstance = new AllowedInstance<Kingdom>(kingdom))
            {
                GameLoopRunner.RunOnMainThread(() =>
                {
                    kingdom.AddPolicy(policy);
                }, true);
            }
        }
    }
}