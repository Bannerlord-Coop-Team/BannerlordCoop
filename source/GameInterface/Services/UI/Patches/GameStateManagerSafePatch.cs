using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch]
internal class GameStateManagerSafePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<GameStateManagerSafePatch>();

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.PushState))]
    [HarmonyFinalizer]
    static System.Exception PushState_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI GameStateManager.PushState exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur ouverture écran (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.PopState))]
    [HarmonyFinalizer]
    static System.Exception PopState_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI GameStateManager.PopState exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur fermeture écran (ignorée)"));
            return null;
        }
        return null;
    }
}
