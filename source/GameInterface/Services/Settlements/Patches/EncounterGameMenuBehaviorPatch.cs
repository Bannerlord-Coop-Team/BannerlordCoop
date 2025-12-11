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

    [HarmonyPatch(typeof(GameMenu))]
    public class GameMenuActivatePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(GameMenu.ActivateGameMenu))]
        public static bool Prefix(ref string menuId)
        {
            if (!string.IsNullOrEmpty(menuId)) return true;

            var logger = Common.Logging.LogManager.GetLogger<GameMenuActivatePatch>();

            var settlement = TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.EncounterSettlement;
            var candidates = new System.Collections.Generic.List<string>(6);
            if (settlement != null)
            {
                if (settlement.IsTown)
                {
                    candidates.Add("town_outside");
                    candidates.Add("town");
                }
                else if (settlement.IsCastle)
                {
                    candidates.Add("castle_outside");
                    candidates.Add("castle");
                }
                else if (settlement.IsVillage)
                {
                    candidates.Add("village_outside");
                    candidates.Add("village");
                }
            }
            candidates.Add("town_outside");
            candidates.Add("castle_outside");
            candidates.Add("village_outside");
            var chosen = candidates.Count > 0 ? candidates[0] : "town_outside";
            menuId = chosen;
            logger.Information("GameMenu.ActivateGameMenu menuId réparé: {id}", chosen);
            return true;
        }
    }
}
