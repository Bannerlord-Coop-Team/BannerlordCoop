using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Settlements.Patches.Disable;


[HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior))]
internal class DisableGarrisonTroopsCampaignBehavior
{
    [HarmonyPatch(nameof(GarrisonTroopsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(GarrisonTroopsCampaignBehavior.OnSettlementEntered))]
    [HarmonyPrefix]
    public static bool OnSettlementEnteredPrefix(MobileParty mobileParty)
    {
        return !mobileParty.IsPlayerParty();
    }
}
