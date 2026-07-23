using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches.Disable;

[HarmonyPatch(typeof(BannerlordConfig))]
internal class DisableOrderSlowdownPatch
{
    [HarmonyPatch(nameof(BannerlordConfig.SlowDownOnOrder), MethodType.Getter)]
    [HarmonyPostfix]
    private static void PostfixSlowDownOnOrder(ref bool __result)
    {
        __result = false;
    }
}
