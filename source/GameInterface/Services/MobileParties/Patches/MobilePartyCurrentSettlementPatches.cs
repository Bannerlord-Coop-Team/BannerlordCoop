using GameInterface.Services.Kingdoms;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty), "set_CurrentSettlement")]
internal static class MobilePartyCurrentSettlementPatches
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool CurrentSettlementPrefix(MobileParty __instance, ref Settlement value)
    {
        if (value != null) return true;
        if (!KingdomCreationSettlementTracker.TryGetTrackedSettlement(__instance, out _)) return true;

        return false;
    }
}
