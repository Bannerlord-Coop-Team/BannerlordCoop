using Common.Logging;
using HarmonyLib;
using SandBox.GauntletUI.Encyclopedia;
using Serilog;
using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Closes an encyclopedia whose page threw while opening, so no later close can fail forever.
/// </summary>
[HarmonyPatch]
internal class EncyclopediaFailedOpenPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<EncyclopediaFailedOpenPatches>();

    [HarmonyPatch(typeof(GauntletMapEncyclopediaView), "ExecuteLink")]
    [HarmonyFinalizer]
    internal static Exception Finalizer_ExecuteLink(GauntletMapEncyclopediaView __instance, Exception __exception)
    {
        if (__exception == null) return null;

        EncyclopediaData data = __instance._encyclopediaData;

        // SetEncyclopediaPage builds the layer before the page, so a page that throws leaves
        // _activeGauntletMovie null while the encyclopedia still counts as open. Left in place it
        // is closed on the next game state change, and that close is the one that never succeeds.
        if (data == null || data._activeGauntletMovie != null) return __exception;

        Logger.Error(__exception, "Failed to open the encyclopedia; closing the unusable page");

        try
        {
            // RemoveLayer finalizes the layer without clearing ScreenManager.FocusedLayer, so the
            // next TrySetFocus would call HandleLoseFocus on a dead layer. Drop focus while the
            // layer can still handle it.
            data._activeGauntletLayer.IsFocusLayer = false;
            ScreenManager.TryLoseFocus(data._activeGauntletLayer);

            __instance.CloseEncyclopedia();
        }
        catch (Exception closeException)
        {
            // Rethrowing would abort the rest of the UI tick and gain nothing, because the detach
            // below has already broken the retry loop that is worth preventing.
            Logger.Error(closeException, "Failed to close the encyclopedia that could not open");
        }
        finally
        {
            __instance._encyclopediaData = null;
            __instance.IsEncyclopediaOpen = false;
        }

        return null;
    }
}

/// <summary>
/// Keeps a missing movie identifier from breaking the gauntlet layer it was asked to release.
/// </summary>
[HarmonyPatch(typeof(GauntletLayer), nameof(GauntletLayer.ReleaseMovie))]
internal class ReleaseMissingMovieRobustnessPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<ReleaseMissingMovieRobustnessPatch>();

    // Vanilla reports an unknown identifier through Debug.FailedAssert, which reads
    // identifier.MovieName and so throws before it can report a null one. Skipping the call leaves
    // every caller that owns a real movie untouched, and lets a caller holding none finish closing.
    [HarmonyPrefix]
    internal static bool SkipMissingIdentifier(GauntletMovieIdentifier identifier)
    {
        if (identifier != null) return true;

        // This guard covers every gauntlet layer, not just the encyclopedia, so say when it fires.
        // A silent skip would hide the next screen that loses track of its movie the same way.
        Logger.Error("Skipped releasing a null movie identifier; a layer was closed without one");

        return false;
    }
}
