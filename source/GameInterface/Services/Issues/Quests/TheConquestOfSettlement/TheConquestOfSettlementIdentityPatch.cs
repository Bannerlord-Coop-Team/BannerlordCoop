using Common.Util;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Quests.TheConquestOfSettlement;

using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Quest), MethodType.Constructor, new[] { typeof(string), typeof(Hero), typeof(Settlement), typeof(CampaignTime), typeof(int) })]
internal static class TheConquestOfSettlementIdentityPatch
{
    [HarmonyPostfix]
    private static void Postfix(Quest __instance, string questId)
    {
        using (new AllowedThread())
        {
            __instance.StringId = questId;
        }
    }
}
