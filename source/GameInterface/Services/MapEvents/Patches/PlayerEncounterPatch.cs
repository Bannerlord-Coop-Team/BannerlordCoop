using System;
using System.Collections.Generic;
using System.Text;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches
{
    /// <summary>
    /// Patches the StartBattle in PlayerEncounter, only runs on local client
    /// </summary>
    [HarmonyPatch(typeof(PlayerEncounter))]
    public class PlayerEncounterPatch
    {
        [HarmonyPatch("StartBattleInternal")]
        [HarmonyPrefix]
        public static bool Prefix(ref PlayerEncounter __instance)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (ModInformation.IsServer) return true;

            var message = new PlayerStartBattle();

            MessageBroker.Instance.Publish(__instance, message);

            return false;
        }
    }

    [HarmonyPatch(typeof(EncounterGameMenuBehavior))]
    public class TestPatching2
    {
        [HarmonyPatch(typeof(MapEventHelper))]
        public class Testing3
        {
            [HarmonyPatch("CanLeaveBattle")]
            [HarmonyPrefix]
            public static void Prefix(MobileParty mobileParty)
            {
                if (ModInformation.IsClient)
                {
                    ;
                }
            }
        }
    }
}
