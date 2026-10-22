using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Framework.Gating;

// These handlers create or end issues, should only be ran on server
[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class DisableIssuesCampaignBehavior
{
    static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.OnSettlementDailyTick)),
        AccessTools.Method(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.DailyTickClan)),
        AccessTools.Method(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.OnGameLoaded)),
    };

    static bool Prefix() => ModInformation.IsServer;
}
