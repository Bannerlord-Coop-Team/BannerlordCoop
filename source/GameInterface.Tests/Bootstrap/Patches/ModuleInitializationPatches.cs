using HarmonyLib;
using System.IO;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

namespace GameInterface.Tests.Bootstrap.Patches;

[HarmonyPatch(typeof(ApplicationVersion), nameof(ApplicationVersion.FromParametersFile))]
internal static class TestApplicationVersionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref ApplicationVersion __result)
    {
        var module = new ModuleInfo();
        module.LoadWithFullPath(Path.Combine(BasePath.Name, "Modules", "Native"));
        __result = module.Version;
        return false;
    }
}
