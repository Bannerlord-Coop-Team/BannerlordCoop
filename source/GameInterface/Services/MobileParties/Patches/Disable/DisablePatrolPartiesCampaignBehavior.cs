using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;


[HarmonyPatch]
internal class DisablePatrolPartiesCampaignBehavior
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PatrolPartiesCampaignBehavior), nameof(PatrolPartiesCampaignBehavior.DailyTickSettlement));
        yield return AccessTools.Method(typeof(PatrolPartiesCampaignBehavior), nameof(PatrolPartiesCampaignBehavior.OnSettlementOwnerChangedEvent));
        yield return AccessTools.Method(typeof(PatrolPartiesCampaignBehavior), nameof(PatrolPartiesCampaignBehavior.AiHourlyTick));
        yield return AccessTools.Method(typeof(PatrolPartiesCampaignBehavior), nameof(PatrolPartiesCampaignBehavior.SettlementEntered));
        yield return AccessTools.Method(typeof(PatrolPartiesCampaignBehavior), nameof(PatrolPartiesCampaignBehavior.OnSettlementLeft));
        yield return AccessTools.Method(typeof(PatrolPartiesCampaignBehavior), nameof(PatrolPartiesCampaignBehavior.MobilePartyDestroyed));
        yield return AccessTools.Method(typeof(PatrolPartiesCampaignBehavior), nameof(PatrolPartiesCampaignBehavior.MobilePartyCreated));
        yield return AccessTools.Method(typeof(PatrolPartiesCampaignBehavior), nameof(PatrolPartiesCampaignBehavior.HourlyTickParty));
        yield return AccessTools.Method(typeof(PatrolPartiesCampaignBehavior), nameof(PatrolPartiesCampaignBehavior.OnBuildingLevelChanged));
    }

    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
