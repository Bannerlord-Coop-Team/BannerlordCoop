using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(PartiesBuyFoodCampaignBehavior))]
internal class DisablePartiesBuyFoodCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesBuyFoodCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
