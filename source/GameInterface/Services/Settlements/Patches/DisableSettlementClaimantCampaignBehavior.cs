using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches;


[HarmonyPatch(typeof(SettlementClaimantCampaignBehavior))]
internal class DisableSettlementClaimantCampaignBehavior
{
    [HarmonyPatch(nameof(SettlementClaimantCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
