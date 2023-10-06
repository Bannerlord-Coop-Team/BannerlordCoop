﻿using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.MobileParties.Patches;
using HarmonyLib;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(ChangeClanLeaderAction), "ApplyInternal")]
    public class ClanLeaderChangePatch
    {
        private static readonly AllowedInstance<Clan> AllowedInstance = new AllowedInstance<Clan>();

        private static readonly Action<Clan, Hero> ApplyInternal =
            typeof(ChangeClanLeaderAction)
            .GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Static)
            .BuildDelegate<Action<Clan, Hero>>();

        static bool Prefix(Clan clan, Hero newLeader = null)
        {
            if (AllowedInstance.IsAllowed(clan)) return true;

            MessageBroker.Instance.Publish(clan, new ClanLeaderChange(clan.StringId, newLeader.StringId));

            return false;
        }
        public static void RunOriginalChangeClanLeader(Clan clan, Hero newLeader = null)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = clan;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    ApplyInternal.Invoke(clan, newLeader);
                }, true);
            }
        }
    }
}
