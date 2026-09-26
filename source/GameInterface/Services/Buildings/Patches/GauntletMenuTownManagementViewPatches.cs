using HarmonyLib;
using SandBox.GauntletUI.Menu;
using GameInterface.Services.Settlements;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Buildings.Patches;

[HarmonyPatch(typeof(GauntletMenuTownManagementView))]
internal class TownManagementViewPatches
{
    public static GauntletMenuTownManagementView Current;

    [HarmonyPatch(nameof(GauntletMenuTownManagementView.OnInitialize))]
    [HarmonyPostfix]
    public static void OnInitializePostfix(GauntletMenuTownManagementView __instance)
    {
        Current = __instance;
    }

    [HarmonyPatch(nameof(GauntletMenuTownManagementView.OnFrameTick))]
    [HarmonyPrefix]
    public static bool OnFrameTickPrefix(GauntletMenuTownManagementView __instance)
    {
        if (SettlementMenuAccess.CanUseSettlement(Hero.MainHero, __instance._dataSource?._settlement)) return true;

        // ExecuteDone commits the local building queue, so close the view directly.
        __instance.MenuViewContext.CloseTownManagement();
        return false;
    }

    [HarmonyPatch(nameof(GauntletMenuTownManagementView.OnFinalize))]
    [HarmonyPostfix]
    public static void OnFinalizePostfix()
    {
        Current = null;
    }
}
