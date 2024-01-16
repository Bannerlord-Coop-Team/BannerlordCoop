using GameInterface.Services.MobileParties.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;

namespace GameInterface.Services.Settlements.Patches
{
    [HarmonyPatch(typeof(EncounterGameMenuBehavior))]
    public class EncounterGameMenuBehaviorPatch
    {
        //These patches disables opening of these menus if EncounterSettlement is null as it seems to get called multiple times.
        [HarmonyPrefix]
        [HarmonyPatch("game_menu_town_outside_on_init")]
        public static bool Prefix(MenuCallbackArgs args)
        {
            if (PlayerEncounter.EncounterSettlement != null) return true;

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("game_menu_town_town_besiege_on_condition")]
        public static bool CheckFortificationEncounterSettlement(MenuCallbackArgs args)
        {
            if (PlayerEncounter.EncounterSettlement != null) return true;

            return false;
        }
    }
}
