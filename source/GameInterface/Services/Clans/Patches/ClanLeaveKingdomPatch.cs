using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(Clan), nameof(Clan.ClanLeaveKingdom))]
    public class ClanLeaveKingdomPatch
    {
        private static AllowedInstance<Clan> _allowedInstance;

        static bool Prefix(ref Clan __instance, bool giveBackFiefs)
        {
            if (__instance == _allowedInstance?.Instance) return true;
            
            MessageBroker.Instance.Publish(__instance, new ClanLeaveKingdom(__instance, giveBackFiefs));

            return false;
        }
        public static void RunOriginalClanLeaveKingdom(Clan clan, bool giveBackFiefs)
        {
            using (_allowedInstance = new AllowedInstance<Clan>(clan))
            {
                GameLoopRunner.RunOnMainThread(() =>
                {
                    clan.ClanLeaveKingdom(giveBackFiefs);
                }, true);
            }
        }
    }
}
