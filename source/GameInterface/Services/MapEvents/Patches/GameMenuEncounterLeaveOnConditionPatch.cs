using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// MapEvents are already destroyed when a client leaves a battle.
/// Skip vanilla check to allow the player to leave if the MapEvent has already been destroyed.
/// </summary>
[HarmonyPatch(typeof(EncounterGameMenuBehavior))]
internal class GameMenuEncounterLeaveOnConditionPatch
{
    [HarmonyPatch(nameof(EncounterGameMenuBehavior.game_menu_encounter_leave_on_condition))]
    [HarmonyPrefix]
    public static bool GameMenuEncounterLeaveOnConditionPrefix(EncounterGameMenuBehavior __instance, ref bool __result, MenuCallbackArgs args)
    {
        if (MobileParty.MainParty.MapEvent != null) return true;

        __result = true;

        return false;
    }
}