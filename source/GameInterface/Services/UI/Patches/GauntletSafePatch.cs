using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch]
internal class GauntletSafePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<GauntletSafePatch>();

    [HarmonyPatch(typeof(GauntletLayer), nameof(GauntletLayer.LoadMovie))]
    [HarmonyFinalizer]
    static System.Exception LoadMovie_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI GauntletLayer.LoadMovie exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur chargement UI (ignorée)"));
            return null;
        }
        return null;
    }
}
