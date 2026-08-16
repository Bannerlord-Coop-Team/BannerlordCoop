using HarmonyLib;
using SandBox.GauntletUI.Menu;

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
}