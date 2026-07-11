using HarmonyLib;
using System.IO;
using System.Reflection;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

namespace GameInterface.Tests.Bootstrap.Patches;

/// <summary>
/// The testhost has no launcher parameters file, so resolve the installed game version from the
/// Native module metadata used by the same test bootstrap.
/// </summary>
[HarmonyPatch(typeof(ApplicationVersion), nameof(ApplicationVersion.FromParametersFile))]
internal static class TestApplicationVersionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref ApplicationVersion __result)
    {
        var nativeModule = new ModuleInfo();
        nativeModule.LoadWithFullPath(Path.Combine(BasePath.Name, "Modules", "Native"));
        __result = nativeModule.Version;
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
/// The testhost has no native sound manager. Skip the visual's static sound-event lookup so MapEvent
/// tests can exercise Gauntlet publication and callback ordering in-process.
/// </summary>
[HarmonyPatch]
internal static class TestGauntletMapEventVisualInitializationPatch
{
    private static MethodBase TargetMethod()
    {
        return Assembly.Load("SandBox.GauntletUI")
            .GetType("SandBox.GauntletUI.Map.GauntletMapEventVisual", throwOnError: true)
            .TypeInitializer;
    }

    [HarmonyPrefix]
    private static bool Prefix() => false;
}
