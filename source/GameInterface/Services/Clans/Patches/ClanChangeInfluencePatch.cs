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
    [HarmonyPatch(typeof(ChangeClanInfluenceAction), "ApplyInternal")]
    public class ClanChangeInfluencePatch
    {
        private static readonly AllowedInstance<Clan> AllowedInstance = new AllowedInstance<Clan>();

        static bool Prefix(Clan clan, float amount)
        {
            if (AllowedInstance.IsAllowed(clan)) return true;

            MessageBroker.Instance.Publish(clan, new ChangeClanInfluence(clan.StringId, amount));

            return false;
        }

        public static void RunOriginalChangeClanInfluence(Clan clan, float amount)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = clan;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    ChangeClanInfluenceAction.Apply(clan, amount);
                }, true);
            }
        }
    }
}
