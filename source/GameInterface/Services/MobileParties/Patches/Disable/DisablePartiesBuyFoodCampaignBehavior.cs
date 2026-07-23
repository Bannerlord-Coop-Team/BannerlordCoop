using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(PartiesBuyFoodCampaignBehavior))]
internal class DisablePartiesBuyFoodCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesBuyFoodCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(PartiesBuyFoodCampaignBehavior))]
internal class PartiesBuyFoodCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PartiesBuyFoodCampaignBehavior.BuyFoodInternal))]
    [HarmonyPrefix]
    public static bool BuyFoodInternalPrefix(PartiesBuyFoodCampaignBehavior __instance, MobileParty mobileParty, Settlement settlement, int numberOfFoodItemsNeededToBuy)
    {
        return mobileParty != null && !mobileParty.IsPlayerParty();
    }
}