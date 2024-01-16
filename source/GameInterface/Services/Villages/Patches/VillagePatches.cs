using Common;
using Common.Messaging;
using Common.Util;
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
    private static bool DailyTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(Village.VillageState), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool VillageStatePrefix(ref Village __instance)
    {
        if(AllowedThread.IsThisThreadAllowed()) return true;
        if (ModInformation.IsClient) return false;
        
        var message = new VillageStateChanged(__instance.Settlement.StringId, (int)__instance.VillageState);
        MessageBroker.Instance.Publish(__instance, message);    
        return true;
    }

    public static void RunVillageStateChange(Village village, VillageStates state)
    {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    village.VillageState = state;
                }
            });
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
