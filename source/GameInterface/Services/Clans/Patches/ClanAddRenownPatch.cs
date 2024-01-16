using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
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
            if (value == 0f) return false;

            if (AllowedInstance.IsAllowed(__instance)) return true;

            if (PolicyProvider.AllowOriginalCalls) return true;

            // On the client if it is not the player client skip the call
            if (ModInformation.IsClient && __instance != Clan.PlayerClan) return false;

            CallStackValidator.Validate(__instance, AllowedInstance);

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
