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
    [HarmonyPatch(typeof(DestroyClanAction), "ApplyInternal")]
    public class ClanDestroyPatch
    {
        private static readonly AllowedInstance<Clan> AllowedInstance = new AllowedInstance<Clan>();

        private static readonly Action<Clan, int> ApplyInternal =
            typeof(DestroyClanAction)
            .GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Static)
            .BuildDelegate<Action<Clan, int>>();

        static bool Prefix(Clan destroyedClan, int details)
        {
            if (AllowedInstance.IsAllowed(destroyedClan)) return true;

            MessageBroker.Instance.Publish(destroyedClan, new DestroyClan(destroyedClan.StringId, details));

            return false;
        }

        public static void RunOriginalDestroyClan(Clan clan, int details)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = clan;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    ApplyInternal.Invoke(clan, details);
                }, true);
            }
        }
    }
}
