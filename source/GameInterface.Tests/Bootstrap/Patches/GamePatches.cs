using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Tests.Bootstrap.Patches
{
    [HarmonyPatch(typeof(Game), "InitializeParameters")]
    internal class GamePatches
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            return false;
        }
    }
}
