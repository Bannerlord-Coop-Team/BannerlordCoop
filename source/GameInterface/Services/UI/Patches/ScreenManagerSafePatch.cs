using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch]
internal class ScreenManagerSafePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<ScreenManagerSafePatch>();

    [HarmonyPatch(typeof(ScreenManager), nameof(ScreenManager.PushScreen))]
    [HarmonyFinalizer]
    static System.Exception PushScreen_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI ScreenManager.PushScreen exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur ouverture écran (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(ScreenManager), nameof(ScreenManager.PopScreen))]
    [HarmonyFinalizer]
    static System.Exception PopScreen_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI ScreenManager.PopScreen exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur fermeture écran (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(ScreenManager), nameof(ScreenManager.TrySetFocus))]
    [HarmonyFinalizer]
    static System.Exception TrySetFocus_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI ScreenManager.TrySetFocus exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur focus écran (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(ScreenManager), nameof(ScreenManager.TryLoseFocus))]
    [HarmonyFinalizer]
    static System.Exception TryLoseFocus_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI ScreenManager.TryLoseFocus exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur perte focus écran (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(ScreenBase), nameof(ScreenBase.AddLayer))]
    [HarmonyFinalizer]
    static System.Exception AddLayer_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI ScreenBase.AddLayer exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur ajout couche UI (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(ScreenBase), nameof(ScreenBase.RemoveLayer))]
    [HarmonyFinalizer]
    static System.Exception RemoveLayer_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI ScreenBase.RemoveLayer exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur retrait couche UI (ignorée)"));
            return null;
        }
        return null;
    }
}
