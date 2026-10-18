using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Actions;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;
using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;

[HarmonyPatch]
internal static class LesserNobleRevoltConsequencePatches
{
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.AlternativeSolutionStartConsequence))]
    [HarmonyPrefix]
    private static bool AlternativeStart(IssueBase __instance) => __instance is not Issue || ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPrefix]
    private static bool FinalizeParty(Quest __instance)
    {
        if (ModInformation.IsServer && __instance._lesserNobleParty?.IsActive == true)
        {
            DestroyPartyAction.Apply(null, __instance._lesserNobleParty);
        }

        return false;
    }
}
