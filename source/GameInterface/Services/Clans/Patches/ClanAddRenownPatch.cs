using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.GameDebug.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(Clan), nameof(Clan.AddRenown))]
    public class ClanAddRenownPatch
    {
        private static readonly AllowedInstance<Clan> AllowedInstance = new AllowedInstance<Clan>();

        static bool Prefix(ref Clan __instance, float value, bool shouldNotify = true)
        {
            CallStackValidator.Validate(__instance, AllowedInstance);

            if (AllowedInstance.IsAllowed(__instance)) return true;

            MessageBroker.Instance.Publish(__instance, new ClanRenownAdded(__instance.StringId, value, shouldNotify));

            return false;
        }

        public static void RunOriginalAddRenown(Clan clan, float amount, bool shouldNotify)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = clan;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    clan.AddRenown(amount, shouldNotify);
                }, true);
            }
        }
    }
}
