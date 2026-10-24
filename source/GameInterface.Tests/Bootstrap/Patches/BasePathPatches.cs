using System;
using System.IO;
using HarmonyLib;
using TaleWorlds.Library;

namespace GameInterface.Tests.Bootstrap.Patches;

/// <summary>
/// Points the game's install root at BANNERLORD_DIR so the test bootstrap loads real modules from the install.
/// </summary>
[HarmonyPatch(typeof(BasePath))]
internal class BasePathPatches
{
    private const string BannerlordDirVariable = "BANNERLORD_DIR";

    /// <summary>Returns BANNERLORD_DIR with a trailing separator instead of the game's exe-relative root.</summary>
    [HarmonyPatch(nameof(BasePath.Name), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool NamePrefix(ref string __result)
    {
        string? bannerlordDir = Environment.GetEnvironmentVariable(BannerlordDirVariable);
        if (string.IsNullOrEmpty(bannerlordDir))
            throw new InvalidOperationException($"{BannerlordDirVariable} is not set; run runmefirst.cmd to point it at the Bannerlord install.");

        bool hasTrailingSeparator = bannerlordDir.EndsWith('/') || bannerlordDir.EndsWith('\\');
        __result = hasTrailingSeparator ? bannerlordDir : bannerlordDir + Path.DirectorySeparatorChar;
        return false;
    }
}
