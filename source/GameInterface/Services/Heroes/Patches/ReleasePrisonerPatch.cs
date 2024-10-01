using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Heroes.Patches
{
    [HarmonyPatch(typeof(EndCaptivityAction))]
    public class ReleasePrisonerPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("ApplyInternal")]
        private static bool Prefix(Hero prisoner, EndCaptivityDetail detail, Hero facilitatior = null)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient && prisoner != Hero.MainHero) return false;

            MessageBroker.Instance.Publish(prisoner, new PrisonerReleased(
                prisoner.StringId,
                (int)detail,
                facilitatior.StringId));

            return ModInformation.IsServer;
        }

        public static void RunOriginalApplyInternal(Hero prisoner, EndCaptivityDetail detail, Hero facilitator = null)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    EndCaptivityAction.ApplyInternal(prisoner, detail, facilitator);
                }
            });
        }
    }
}