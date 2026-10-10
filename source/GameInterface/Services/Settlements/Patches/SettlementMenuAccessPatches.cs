using HarmonyLib;
using SandBox.GauntletUI;
using SandBox.GauntletUI.Menu;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Settlements.Patches;

[HarmonyPatch]
internal class SettlementMenuAccessPatches
{
    [HarmonyPatch(typeof(GameMenuOption), nameof(GameMenuOption.GetConditionsHold))]
    [HarmonyPostfix]
    public static void GetConditionsHoldPostfix(GameMenuOption __instance, bool __result)
    {
        if (!__result || !SettlementMenuAccess.IsManagedMenu(__instance.IdString)) return;
        if (!ContainerProvider.TryResolve<ISettlementMenuAccess>(out var access)) return;

        if (access.IsInUse(Settlement.CurrentSettlement, __instance.IdString))
        {
            __instance.SetEnable(false);
            __instance.Tooltip = GameTexts.FindText("str_coop_clan_settlement_menu_in_use");
        }
    }

    [HarmonyPatch(typeof(GameMenuOption), nameof(GameMenuOption.RunConsequence))]
    [HarmonyPrefix]
    public static bool RunConsequencePrefix(GameMenuOption __instance, MenuContext menuContext)
    {
        if (!SettlementMenuAccess.IsManagedMenu(__instance.IdString)) return true;
        return ContainerProvider.TryResolve<ISettlementMenuAccess>(out var access) && access.TryOpen(__instance, menuContext);
    }

    [HarmonyPatch(typeof(GauntletPartyScreen), "TaleWorlds.Core.IGameStateListener.OnFinalize")]
    [HarmonyPostfix]
    public static void PartyScreenClosedPostfix()
    {
        Close("manage_garrison");
        Close("town_prison_manage_prisoners");
    }

    [HarmonyPatch(typeof(GauntletInventoryScreen), nameof(GauntletInventoryScreen.OnFinalize))]
    [HarmonyPostfix]
    public static void InventoryScreenClosedPostfix() => Close("open_stash");

    [HarmonyPatch(typeof(GauntletMenuTownManagementView), nameof(GauntletMenuTownManagementView.OnFinalize))]
    [HarmonyPostfix]
    public static void TownManagementClosedPostfix() => Close("manage_production");

    private static void Close(string menuId)
    {
        if (ContainerProvider.TryResolve<ISettlementMenuAccess>(out var access)) access.Close(menuId);
    }
}
