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
        static bool Prefix(ref Clan __instance, float value, bool shouldNotify = true)
        {
            if (value == 0f) return false;

            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (PolicyProvider.AllowOriginalCalls) return true;

            // On the client if it is not the player client skip the call
            if (ModInformation.IsClient && __instance != Clan.PlayerClan) return false;

            MessageBroker.Instance.Publish(__instance, new ClanRenownAdded(__instance.StringId, value, shouldNotify));

            return false;
        }

        public static void RunOriginalAddRenown(Clan clan, float amount, bool shouldNotify)
        {
                GameLoopRunner.RunOnMainThread(() =>
                {
                    using(new AllowedThread())
                    {
                        clan.AddRenown(amount, shouldNotify);
                    }
                }, true);
        }
    }
}
