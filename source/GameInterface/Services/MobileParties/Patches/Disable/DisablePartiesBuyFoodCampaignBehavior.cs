using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PartiesBuyFoodCampaignBehavior))]
internal class DisablePartiesBuyFoodCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesBuyFoodCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
