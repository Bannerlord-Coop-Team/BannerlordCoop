using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;
using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;

[HarmonyPatch]
internal static class LesserNobleRevoltAcceptPatches
{
    [HarmonyPatch(typeof(Issue), nameof(Issue.GenerateIssueQuest))]
    [HarmonyPrefix]
    private static bool GenerateQuest(Issue __instance, ref QuestBase __result)
    {
        return true;
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddDialogs))]
    [HarmonyPrefix]
    private static bool AddDialogs(QuestBase __instance)
    {
        if (__instance is not Quest quest)
        {
            return true;
        }

        if (ModInformation.IsServer)
        {
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.SpawnLesserNobleParty))]
    [HarmonyPrefix]
    private static bool SpawnParty() => true;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnCanHeroBecomePrisonerInfoIsRequested))]
    [HarmonyPrefix]
    private static bool CanBecomePrisoner(Quest __instance, Hero hero, ref bool result)
    {
        // The revolt owner must stay in the field while its battle is pending.
        if (hero == Hero.MainHero && __instance._checkForEventEnd)
        {
            result = false;
        }

        return false;
    }
}
