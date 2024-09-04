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
        static bool Prefix(Clan clan, Hero newLeader = null)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient && clan != Clan.PlayerClan) return false;

            MessageBroker.Instance.Publish(clan, new ClanLeaderChanged(clan.StringId, newLeader.StringId));

            return false;
        }
        public static void RunOriginalChangeClanLeader(Clan clan, Hero newLeader = null)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    ChangeClanLeaderAction.ApplyInternal(clan, newLeader);
                }
            });
        }
    }
}
