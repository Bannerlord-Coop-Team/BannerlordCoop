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
    [HarmonyPatch(typeof(Clan), nameof(Clan.ChangeClanName))]
    public class ClanNameChangePatch
    {
        private static AllowedInstance<Clan> _allowedInstance;

        static bool Prefix(ref Clan __instance, TextObject name, TextObject informalName)
        {
            if (__instance == _allowedInstance?.Instance) return true;
            
            MessageBroker.Instance.Publish(__instance, new ClanNameChange(__instance, name.ToString(), informalName.ToString()));

            return false;
        }
        public static void RunOriginalChangeClanName(Clan clan, TextObject name, TextObject informalName)
        {
            using (_allowedInstance = new AllowedInstance<Clan>(clan))
            {
                GameLoopRunner.RunOnMainThread(() =>
                {
                    clan.ChangeClanName(name, informalName);
                }, true);
            }
        }
    }
}
