using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(PartiesBuyHorseCampaignBehavior))]
internal class DisablePartiesBuyHorseCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesBuyHorseCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(PartiesBuyHorseCampaignBehavior))]
internal class PartiesBuyHorseCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PartiesBuyHorseCampaignBehavior.OnSettlementEntered))]
    [HarmonyPrefix]
    public static bool OnSettlementEnteredPrefix(PartiesBuyHorseCampaignBehavior __instance, MobileParty mobileParty, Settlement settlement, Hero hero)
    {
        return mobileParty != null && !mobileParty.IsPlayerParty();
    }
}