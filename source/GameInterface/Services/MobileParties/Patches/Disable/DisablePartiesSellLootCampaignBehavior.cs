using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(PartiesSellLootCampaignBehavior))]
internal class DisablePartiesSellLootCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesSellLootCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(PartiesSellLootCampaignBehavior))]
internal class PartiesSellLootCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PartiesSellLootCampaignBehavior.OnSettlementEntered))]
    [HarmonyPrefix]
    public static bool OnSettlementEnteredPrefix(PartiesSellLootCampaignBehavior __instance, MobileParty mobileParty, Settlement settlement, Hero hero)
    {
        return mobileParty != null && !mobileParty.IsPlayerParty();
    }
}