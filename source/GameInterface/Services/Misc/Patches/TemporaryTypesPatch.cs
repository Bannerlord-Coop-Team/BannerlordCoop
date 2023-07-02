using HarmonyLib;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Misc.Patches;

/// <summary>
/// Fixes a bug with TemporaryTypes
/// </summary>
[HarmonyPatch(typeof(MBObjectManager), "RemoveTemporaryTypes")]
class TemporaryTypesPatch
{
    static bool Prefix()
    {
        return false;
    }
}
