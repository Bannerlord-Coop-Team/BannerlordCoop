using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan.Patches;

[HarmonyPatch]
internal class EscortServerMethodsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[]
        {
            nameof(Quest.OnFinalize), nameof(Quest.OnTimedOut), nameof(Quest.SuccessConsequences),
            nameof(Quest.FailConsequences), nameof(Quest.FailByPlayerHostileConsequences),
            nameof(Quest.OnSettlementEntered), nameof(Quest.OnSettlementLeft), nameof(Quest.OnMapEventEnded),
            nameof(Quest.OnSettlementOwnerChanged),
            nameof(Quest.OnPartyHourlyTick), nameof(Quest.HourlyTick), nameof(Quest.DailyTick),
        })
            yield return AccessTools.DeclaredMethod(typeof(Quest), name);
    }

    [HarmonyPrefix]
    private static bool Prefix() => ModInformation.IsServer;
}
