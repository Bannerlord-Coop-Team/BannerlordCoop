using Autofac;
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
    [HarmonyPatch(typeof(ChangeClanLeaderAction), "ApplyInternal")]
    public class ClanLeaderChangePatch
    {
        private static readonly AllowedInstance<Clan> AllowedInstance = new AllowedInstance<Clan>();

        static bool Prefix(Clan clan, Hero newLeader = null)
        {
            if (AllowedInstance.IsAllowed(clan)) return true;

            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient && clan != Clan.PlayerClan) return false;

            CallStackValidator.Validate(clan, AllowedInstance);

            MessageBroker.Instance.Publish(clan, new ClanLeaderChanged(clan.StringId, newLeader.StringId));

            return false;
        }
        public static void RunOriginalChangeClanLeader(Clan clan, Hero newLeader = null)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = clan;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    ChangeClanLeaderAction.ApplyInternal(clan, newLeader);
                }, true);
            }
        }
    }
}
