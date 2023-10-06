using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(Clan), nameof(Clan.AddRenown))]
    public class ClanAddRenownPatch
    {
        private static readonly AllowedInstance<Clan> AllowedInstance = new AllowedInstance<Clan>();

        static bool Prefix(ref Clan __instance, float value, bool shouldNotify = true)
        {
            if (AllowedInstance.IsAllowed(__instance)) return true;

            MessageBroker.Instance.Publish(__instance, new AddRenown(__instance.StringId, value, shouldNotify));

            return false;
        }

        public static void RunOriginalAddCompanion(Clan clan, float amount, bool shouldNotify)
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
