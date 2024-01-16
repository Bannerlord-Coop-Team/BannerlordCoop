using Common.Messaging;
using GameInterface.Services.Villages.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Settlements.Village;


namespace GameInterface.Services.Villages.Patches;

/// <summary>
/// Disables all functionality for Town
/// </summary>
[HarmonyPatch(typeof(Village))]
internal class VillagePatches
{
    [HarmonyPatch("DailyTick")]
    [HarmonyPrefix]
    private static bool DailyTickPrefix()
    {
        if(ModInformation.IsServer) return true;
        return false;
    }

    [HarmonyPatch(nameof(Village.VillageState), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool VillageStatePrefix(ref Village __instance)
    {
        if (ModInformation.IsServer)
        {
            MessageBroker.Instance.Publish(__instance, new VillageStateChange(__instance));    
            return true;
        }
        return false;
    }

    [HarmonyPatch(nameof(Village.Hearth), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool HearthPrefix()
    {
        return false;
    }

    [HarmonyPatch(nameof(Village.TradeTaxAccumulated), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool TradeTaxAccumulatedPrefix()
    {
        return false;
    }
}
