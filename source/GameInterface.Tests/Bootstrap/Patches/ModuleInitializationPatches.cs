using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

namespace GameInterface.Tests.Bootstrap.Patches;

/// <summary>
/// Keeps the in-process test game bootstrap deterministic when it runs outside the Bannerlord
/// launcher. The testhost has no launcher parameters file, so the vanilla version probe returns an
/// invalid version and newly installed DLC modules terminate the process during compatibility checks.
/// </summary>
[HarmonyPatch(typeof(ApplicationVersion), nameof(ApplicationVersion.FromParametersFile))]
internal static class TestApplicationVersionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref ApplicationVersion __result)
    {
        __result = ApplicationVersion.FromString("v1.4.7");
        return false;
    }
}

/// <summary>
/// E2E tests create several isolated game instances in one process. Vanilla module initialization
/// appends the platform DLC path each time, so remove its previous entry before the next bootstrap.
/// </summary>
[HarmonyPatch(typeof(ModuleHelper), nameof(ModuleHelper.InitializeModules))]
internal static class TestModuleInitializationPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        VirtualFolders.PlatformDLCPaths.Remove("NavalDLC");
    }
}

/// <summary>
/// The testhost has no native sound manager. Skip the visual's static sound-event lookup so aggregate
/// hydration tests can exercise Gauntlet publication and callback ordering in-process.
/// </summary>
[HarmonyPatch]
internal static class TestGauntletMapEventVisualInitializationPatch
{
    private static MethodBase TargetMethod()
    {
        var visualType = AccessTools.TypeByName("SandBox.GauntletUI.Map.GauntletMapEventVisual") ??
            throw new InvalidOperationException("Could not resolve GauntletMapEventVisual");
        return visualType.TypeInitializer ??
            throw new InvalidOperationException("GauntletMapEventVisual had no type initializer");
    }

    [HarmonyPrefix]
    private static bool Prefix() => false;
}
