using HarmonyLib;
using System.IO;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

namespace GameInterface.Tests.Bootstrap.Patches;

[HarmonyPatch(typeof(ApplicationVersion), nameof(ApplicationVersion.FromParametersFile))]
internal static class TestApplicationVersionPatch
{
    // The game version the referenced assemblies come from.
    private const string FallbackVersion = "v1.4.8";

    [HarmonyPrefix]
    private static bool Prefix(ref ApplicationVersion __result)
    {
        var nativePath = Path.Combine(BasePath.Name, "Modules", "Native");

        // CI's game folder has the assemblies but not Native's SubModule.xml.
        if (!File.Exists(Path.Combine(nativePath, "SubModule.xml")))
        {
            __result = ApplicationVersion.FromString(FallbackVersion);
            return false;
        }

        var module = new ModuleInfo();
        module.LoadWithFullPath(nativePath);
        __result = module.Version;
        return false;
    }
}
