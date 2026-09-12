using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches.Disable;

[HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior))]
internal class DisableWaitInSettlementOption
{
    [HarmonyPatch(nameof(PlayerTownVisitCampaignBehavior.game_menu_wait_here_on_condition))]
    [HarmonyPrefix]
    public static bool GameMenuWaitHereOnConditionPrefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}
