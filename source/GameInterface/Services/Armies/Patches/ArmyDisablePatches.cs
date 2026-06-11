using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Patches;

[HarmonyPatch(typeof(Army))]
class ArmyDisablePatches
{
    [HarmonyPatch(nameof(Army.Tick))]
    [HarmonyPrefix]
    private static bool DisableArmyTick() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(Kingdom))]
class CreateArmyDisablePatches
{
    [HarmonyPatch(nameof(Kingdom.CreateArmy))]
    [HarmonyPrefix]
    private static bool DisableArmyTick() => ArmyConfig.Enabled;
}
