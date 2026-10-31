using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(ShipRepairCampaignBehavior))]
internal class ShipRepairCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(ShipRepairCampaignBehavior.DailyTickParty))]
    [HarmonyPrefix]
    public static bool DailyTickPartyPrefix(MobileParty mobileParty)
    {
        if (ModInformation.IsClient) return false;

        return mobileParty == null || !mobileParty.IsPlayerParty();
    }

    [HarmonyPatch(nameof(ShipRepairCampaignBehavior.OnAfterSettlementEnter))]
    [HarmonyPrefix]
    public static bool OnAfterSettlementEnterPrefix(MobileParty mobileParty)
    {
        if (ModInformation.IsClient) return false;

        return mobileParty == null || !mobileParty.IsPlayerParty();
    }

    [HarmonyPatch(nameof(ShipRepairCampaignBehavior.OnShipDestroyed))]
    [HarmonyPrefix]
    public static bool OnShipDestroyedPrefix() => ModInformation.IsServer;
}
