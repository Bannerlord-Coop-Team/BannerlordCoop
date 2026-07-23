using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches.Disable;

[HarmonyPatch(typeof(PoliticalStagnationAndBorderIncidentCampaignBehavior))]
internal class DisablePoliticalStagnationAndBorderIncidentCampaignBehavior
{
    [HarmonyPatch(nameof(PoliticalStagnationAndBorderIncidentCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(PoliticalStagnationAndBorderIncidentCampaignBehavior))]
internal class PoliticalStagnationAndBorderIncidentCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PoliticalStagnationAndBorderIncidentCampaignBehavior.GetThreatValueOfEnemyToSettlement))]
    [HarmonyPrefix]
    public static bool GetThreatValueOfEnemyToSettlementPrefix(PoliticalStagnationAndBorderIncidentCampaignBehavior __instance, ref float __result, MobileParty mobileParty, Settlement settlement)
    {
        float num = __instance.GetThreatValueOfParty(mobileParty);

        if (mobileParty.IsPlayerParty()) // Check for player party
            num *= 2f;
        if (!mobileParty.IsLordParty)
            num *= 0.5f;
        if (mobileParty.DefaultBehavior == AiBehavior.PatrolAroundPoint && mobileParty.TargetSettlement == settlement)
            num *= 2f;
        if (mobileParty.MapEvent != null && mobileParty.MapEvent.IsFieldBattle)
            num = 3f * num;

        __result = num;
        return false;
    }
}
