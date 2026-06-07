using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(FoodConsumptionBehavior))]
internal class DisableFoodConsumptionBehavior
{
    [HarmonyPatch(nameof(FoodConsumptionBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
