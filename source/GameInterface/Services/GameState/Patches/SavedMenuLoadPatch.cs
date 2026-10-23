using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.GameState.Patches;

/// <summary>
/// Keeps the game menu stored in a save from reopening on load when it can't be shown.
/// </summary>
/// <remarks>
/// SandBoxGameManager.OnLoadFinished queues the stored menu, then <see cref="MapState.OnLoadingFinished"/> opens it
/// through <see cref="MapState.EnterMenuMode"/>, which runs the menu's init right away. Town, castle and village menus
/// read <see cref="Settlement.CurrentSettlement"/> without a null check, so a save made in one crashed the server on
/// every boot and joining clients at the end of their load. Based on a fix by Ryvius (@PlueRyvius) in ModderLords.
/// </remarks>
[HarmonyPatch]
internal class SavedMenuLoadPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<SavedMenuLoadPatch>();

    // The outside menus whose background init reads Settlement.CurrentSettlement; this is the handler in the crash.
    private static readonly HashSet<string> OutsideSettlementMenus = new HashSet<string>(
        AccessTools.Method(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.encounter_menu_ui_castle_on_init))
            ?.GetCustomAttributes<GameMenuInitializationHandler>(false)
            .Select(handler => handler.MenuId)
        ?? Enumerable.Empty<string>());

    [HarmonyPatch(typeof(MapState), nameof(MapState.OnLoadingFinished))]
    [HarmonyPrefix]
    static void DropStoredMenu(MapState __instance)
    {
        var menuId = __instance.GameMenuId;
        if (string.IsNullOrEmpty(menuId))
            return;

        if (ModInformation.IsServer)
        {
            // The host must never sit in a menu, and it has to be dropped before EnterMenuMode runs its init.
            // RemoveMainParty has already run, so PlayerEncounter.Finish() would NRE; clear the encounter directly.
            DropMenu(__instance, menuId);
            Campaign.Current.PlayerEncounter = null;
            Campaign.Current.LocationEncounter = null;
            return;
        }

        var menu = Campaign.Current.GameMenuManager.GetGameMenu(menuId);
        if (NeedsSettlement(menu) && Settlement.CurrentSettlement == null)
            DropMenu(__instance, menuId);
    }

    /// <summary>
    /// Whether <paramref name="menu"/> can only be shown inside a settlement: an outside menu whose init reads
    /// the settlement, or a menu with a settlement overlay (town, castle, village and their sub menus).
    /// </summary>
    internal static bool NeedsSettlement(GameMenu menu)
    {
        if (menu == null)
            return false;

        return OutsideSettlementMenus.Contains(menu.StringId) ||
            menu.OverlayType == GameMenu.MenuOverlayType.SettlementWithParties ||
            menu.OverlayType == GameMenu.MenuOverlayType.SettlementWithCharacters ||
            menu.OverlayType == GameMenu.MenuOverlayType.SettlementWithBoth;
    }

    private static void DropMenu(MapState mapState, string menuId)
    {
        mapState.GameMenuId = null;
        Campaign.Current.GameMenuManager.SetNextMenu(null);
        Logger.Information("Dropped stored game menu {MenuId} on load, opening on the campaign map", menuId);
    }
}
