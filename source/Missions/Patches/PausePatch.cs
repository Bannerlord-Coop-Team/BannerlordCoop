using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Missions.Patches
{
    [HarmonyPatch(typeof(GameNetwork), "IsMultiplayer", MethodType.Getter)]
    public class PausePatch
    {
        public static void Postfix(ref bool __result)
        {
            __result = true;
        }
    }
}
