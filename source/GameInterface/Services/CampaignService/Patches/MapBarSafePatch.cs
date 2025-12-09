using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.CampaignService.Patches;

/// <summary>
/// Ajoute des garde‑fous pour éviter les crashes UI de la barre carte
/// </summary>
[HarmonyPatch]
internal class MapBarSafePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapBarSafePatch>();

    [HarmonyPatch(typeof(MapNavigationItemVM), nameof(MapNavigationItemVM.RefreshStates))]
    [HarmonyFinalizer]
    static System.Exception RefreshStates_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI MapNavigationItemVM.RefreshStates exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur menus carte (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(MapBarVM), nameof(MapBarVM.Initialize))]
    [HarmonyFinalizer]
    static System.Exception Initialize_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI MapBarVM.Initialize exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur initialisation barre carte (ignorée)"));
            return null;
        }
        return null;
    }
}

