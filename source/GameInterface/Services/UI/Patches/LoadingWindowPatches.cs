using GameInterface.Services.UI.Interfaces;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Keeps the global loading window visible across game-state transitions while a coop
/// session is starting (a client joining, or the host applying patches before its save loads).
/// </summary>
/// <remarks>
/// The engine destroys and recreates its loading window manager during state changes
/// (e.g. main menu -> load save). On each recreation it re-enables the window only if
/// <c>LoadingWindow.IsLoadingWindowActive</c> is still <see langword="true"/>, and the
/// only thing that sets that flag back to <see langword="false"/> is
/// <see cref="LoadingWindow.DisableGlobalLoadingWindow"/>. So, by keeping the flag active
/// and blocking native disables while <see cref="ForceLoadingWindow"/> is set, the loading
/// screen stays up consistently instead of flickering as the client's local world is
/// swapped for the server world. These patches are applied at boot (see CoopMod) through
/// <see cref="GameInterface.HARMONY_UI_LOADING_CATEGORY"/> so the guard already exists while
/// the host's own PatchAll is still compiling; they do nothing until a coop flow sets
/// <see cref="ForceLoadingWindow"/> or a loading message.
/// </remarks>
[HarmonyPatch(typeof(LoadingWindow))]
[HarmonyPatchCategory(GameInterface.HARMONY_UI_LOADING_CATEGORY)]
internal static class LoadingWindowPatches
{
    /// <summary>
    /// While true, native attempts to disable the global loading window are ignored.
    /// </summary>
    public static bool ForceLoadingWindow { get; set; }

    [HarmonyPatch(nameof(LoadingWindow.DisableGlobalLoadingWindow))]
    [HarmonyPrefix]
    private static bool DisableGlobalLoadingWindowPrefix()
    {
        // Skip the disable entirely while we are forcing the window to remain visible.
        return !ForceLoadingWindow;
    }

    [HarmonyPatch(nameof(LoadingWindow.EnableGlobalLoadingWindow))]
    [HarmonyPostfix]
    private static void EnableGlobalLoadingWindowPostfix()
    {
        LoadingInterface.ApplyCurrentLoadingMessage();
    }
}

/// <summary>
/// Reapplies coop loading text when the engine directly re-enables a recreated
/// loading window manager.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_UI_LOADING_CATEGORY)]
internal static class GauntletDefaultLoadingWindowManagerPatches
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(GauntletDefaultLoadingWindowManager),
            "TaleWorlds.Engine.ILoadingWindowManager.EnableLoadingWindow");
    }

    private static void Postfix()
    {
        LoadingInterface.ApplyCurrentLoadingMessage();
    }
}
