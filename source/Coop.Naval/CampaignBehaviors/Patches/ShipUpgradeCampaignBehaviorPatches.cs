using Common;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(ShipUpgradeCampaignBehavior))]
internal class ShipUpgradeCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(ShipUpgradeCampaignBehavior.OnSettlementEntered))]
    [HarmonyPrefix]
    public static bool OnSettlementEnteredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(ShipUpgradeCampaignBehavior.DailyTickPartyEvent))]
    [HarmonyPrefix]
    public static bool DailyTickPartyEventPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(ShipUpgradeCampaignBehavior.CanPartyUpgradeShips))]
    [HarmonyPostfix]
    public static void CanPartyUpgradeShipsPostfix(MobileParty party, ref bool __result)
    {
        // Override result if clan is a player clan
        if (__result && party.ActualClan.IsPlayerClan())
        {
            __result = false;
        }
    }
}
