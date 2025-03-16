using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Patches
{
    [HarmonyPatch(typeof(Army))]
    class ArmyDisablePatches
    {
        [HarmonyPatch(nameof(Army.Tick))]
        [HarmonyPrefix]
        private static bool DisableArmyTick() => ModInformation.IsServer;
    }
}
