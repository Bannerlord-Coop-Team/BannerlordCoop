using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.GameDebug.Patches;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(DestroyClanAction), "ApplyInternal")]
    public class ClanDestroyPatch
    {
        static bool Prefix(Clan destroyedClan, int details)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient && destroyedClan != Clan.PlayerClan) return false;

            MessageBroker.Instance.Publish(destroyedClan, new ClanDestroyed(destroyedClan.StringId, details));

            return false;
        }

        public static void RunOriginalDestroyClan(Clan clan, int details)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    DestroyClanAction.ApplyInternal(clan, (DestroyClanAction.DestroyClanActionDetails)details);
                }
            });
        }
    }
}
