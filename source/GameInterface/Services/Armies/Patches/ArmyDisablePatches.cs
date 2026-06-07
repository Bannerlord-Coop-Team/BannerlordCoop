using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using GameInterface.Policies;

namespace GameInterface.Services.Armies.Patches;

[HarmonyPatch(typeof(Army))]
class ArmyDisablePatches
{
    [HarmonyPatch(nameof(Army.Tick))]
    [HarmonyPrefix]
    private static bool DisableArmyTick()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}

[HarmonyPatch(typeof(Kingdom))]
class CreateArmyDisablePatches
{
    [HarmonyPatch(nameof(Kingdom.CreateArmy))]
    [HarmonyPrefix]
    private static bool DisableArmyTick()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ArmyConfig.Enabled;
    }
}
