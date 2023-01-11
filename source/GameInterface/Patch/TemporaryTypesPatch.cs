using HarmonyLib;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Patch
{
    [HarmonyPatch(typeof(MBObjectManager), "RemoveTemporaryTypes")]
    class TemporaryTypesPatch
    {
        static bool Prefix()
        {
            return false;
        }        
    }
}
