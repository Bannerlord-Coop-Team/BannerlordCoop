using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches.Disable;

[HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior))]
internal class DisablePlayerTownVisitCampaignBehavior
{
    /// <summary>
    /// Disables entering the arena from the town menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_town_town_arena_on_consequence")]
    static bool DisableArena()
    {
        return false;
    }

    /// <summary>
    /// Disables entering the tavern from the town menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_town_town_tavern_on_consequence")]
    static bool DisableTavern()
    {
        return false;
    }

    /// <summary>
    /// Disables entering the jail from the castle menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_castle_dungeon_on_consequence")]
    static bool DisableCastleDungeon()
    {
        return false;
    }

    /// <summary>
    /// Disables entering the jail from the town menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_town_dungeon_on_consequence")]
    static bool DisableTownDungeon()
    {
        return false;
    }

    /// <summary>
    /// Disables entering the market from the town menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_town_town_market_on_consequence")]
    static bool DisableTownMarket()
    {
        return true;
    }

    /// <summary>
    /// Disables entering the town center from the town menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_town_town_streets_on_consequence")]
    static bool DisableTownCenter()
    {
        return false;
    }

    /// <summary>
    /// Disables entering the town hall from the town menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_town_lordshall_on_consequence")]
    static bool DisableTownHall()
    {
        return false;
    }

    /// <summary>
    /// Disables entering the town keep from the town menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_town_go_to_keep_on_consequence")]
    static bool DisableTownKeep()
    {
        return false;
    }

    /// <summary>
    /// Disables entering the town center from the village menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_village_village_center_on_consequence")]
    static bool DisableVillageCenter()
    {
        return false;
    }

    /// <summary>
    /// Disables entering the castle hall from the castle menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_castle_lordshall_on_consequence")]
    static bool DisableCastleHall()
    {
        return false;
    }

    /// <summary>
    /// Disables entering the garrison management from the town menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_leave_troops_garrison_on_consequece")]
    static bool DisableGarrison()
    {
        return false;
    }

    /// <summary>
    /// Disables entering the town management from the town menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_ui_town_manage_town_on_consequence")]
    static bool DisableTownManagement()
    {
        return false;
    }

    /// <summary>
    /// Disables entering the castle management from the castle menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_ui_town_castle_manage_town_on_consequence")]
    static bool DisableCastleManagement()
    {
        return false;
    }

    /// <summary>
    /// Disables entering the castle walk around from the castle menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_castle_take_a_walk_around_the_castle_on_consequence")]
    static bool DisableCastleWalkAround()
    {
        return false;
    }

    /// <summary>
    /// Disables entering the village buy goods from the village menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_ui_village_buy_good_on_consequence")]
    static bool DisableVillageBuyGoods()
    {
        return true;
    }

    /// <summary>
    /// Disables entering the town stash from the town menu
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("game_menu_town_keep_open_stash_on_consequence")]
    static bool DisableTownStash()
    {
        return false;
    }
}