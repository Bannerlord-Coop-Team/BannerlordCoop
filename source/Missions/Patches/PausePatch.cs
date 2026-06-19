using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions.Patches
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
