using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Pausing.Patches
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
