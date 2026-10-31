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
/// OnLoadingFinished opens the stored menu and runs its init at once, and settlement menus read
/// <see cref="Settlement.CurrentSettlement"/> without a null check, so a save made in one crashed on load.
/// </remarks>
[HarmonyPatch]
internal class SavedMenuLoadPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<SavedMenuLoadPatch>();

    // The outside menus whose background init reads Settlement.CurrentSettlement; the first handler is the one in the
    // crash, and v1.5 moved castle_outside to its own init that reads it too.
    private static readonly HashSet<string> OutsideSettlementMenus = FindOutsideSettlementMenus();

    private static HashSet<string> FindOutsideSettlementMenus()
    {
        var menus = new HashSet<string>(
            new[]
            {
                nameof(EncounterGameMenuBehavior.encounter_menu_ui_castle_on_init),
                nameof(EncounterGameMenuBehavior.castle_outside_on_init),
            }
            .Select(name => AccessTools.Method(typeof(EncounterGameMenuBehavior), name))
            .Where(method => method != null)
            .SelectMany(method => method.GetCustomAttributes<GameMenuInitializationHandler>(false))
            .Select(handler => handler.MenuId));

        if (menus.Count == 0)
            Logger.Warning("Found no menus for encounter_menu_ui_castle_on_init or castle_outside_on_init; stored outside settlement menus will not be dropped on load");

        return menus;
    }

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
