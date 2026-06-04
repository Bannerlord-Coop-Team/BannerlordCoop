using HarmonyLib;
using System.Reflection;
using TaleWorlds.Engine;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Keeps the global loading window visible across game-state transitions while a coop
/// client is joining a server.
/// </summary>
/// <remarks>
/// The engine destroys and recreates its loading window manager during state changes
/// (e.g. main menu -> load save). On each recreation it re-enables the window only if
/// <c>LoadingWindow.IsLoadingWindowActive</c> is still <see langword="true"/>, and the
/// only thing that sets that flag back to <see langword="false"/> is
/// <see cref="LoadingWindow.DisableGlobalLoadingWindow"/>. So, by keeping the flag active
/// and blocking native disables while <see cref="ForceLoadingWindow"/> is set, the loading
/// screen stays up consistently instead of flickering as the client's local world is
/// swapped for the server world.
/// </remarks>
[HarmonyPatch(typeof(LoadingWindow))]
internal static class LoadingWindowPatches
{
    /// <summary>
    /// While true, native attempts to disable the global loading window are ignored.
    /// </summary>
    public static bool ForceLoadingWindow { get; private set; }

    // Setter is non-public on the engine type; resolve it once via reflection.
    private static readonly MethodInfo SetIsLoadingWindowActive =
        AccessTools.PropertySetter(typeof(LoadingWindow), nameof(LoadingWindow.IsLoadingWindowActive));

    [HarmonyPatch(nameof(LoadingWindow.DisableGlobalLoadingWindow))]
    [HarmonyPrefix]
    private static bool DisableGlobalLoadingWindowPrefix()
    {
        // Skip the disable entirely while we are forcing the window to remain visible.
        return !ForceLoadingWindow;
    }

    /// <summary>
    /// Begins forcing the loading window to stay visible. Must run on the main thread.
    /// </summary>
    public static void Begin()
    {
        ForceLoadingWindow = true;

        // Mark the window active even if no manager exists yet (e.g. mid state-transition),
        // so the engine re-enables it the next time the manager is (re)initialized.
        SetIsLoadingWindowActive?.Invoke(null, new object[] { true });

        // Show it immediately if a manager is already present.
        LoadingWindow.EnableGlobalLoadingWindow();
    }

    /// <summary>
    /// Stops forcing and hides the loading window. Must run on the main thread.
    /// </summary>
    public static void End()
    {
        ForceLoadingWindow = false;
        LoadingWindow.DisableGlobalLoadingWindow();
    }
}
