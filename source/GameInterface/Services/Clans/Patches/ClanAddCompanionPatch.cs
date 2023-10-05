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
    [HarmonyPatch(typeof(AddCompanionAction), "ApplyInternal")]
    public class ClanAddCompanionPatch
    {
        private static readonly AllowedInstance<Hero> AllowedInstance = new AllowedInstance<Hero>();

        static bool Prefix(Clan clan, Hero companion)
        {
            if (AllowedInstance.IsAllowed(companion)) return true;

            MessageBroker.Instance.Publish(clan, new AddCompanion(clan, companion));

            return false;
        }

        public static void RunOriginalAddCompanion(Clan clan, Hero companion)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = companion;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    AddCompanionAction.Apply(clan, companion);
                }, true);
            }
        }
    }
}
