using Common;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue;

namespace GameInterface.Services.Issues.Quests.LordsNeedsTutor.Patches;

[HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior), nameof(LordsNeedsTutorIssueBehavior.OnCheckForIssue))]
internal class TutorIssueGenerationPatch
{
    private static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior))]
internal class TutorPlayerClanEligibilityPatch
{
    [HarmonyPatch(nameof(LordsNeedsTutorIssueBehavior.SuitableCondition))]
    [HarmonyPostfix]
    private static void SuitablePostfix(Hero hero, ref bool __result)
    {
        if (__result && ContainerProvider.TryResolve<IPlayerManager>(out var players) && players.Contains(hero.Clan)) __result = false;
    }

    [HarmonyPatch(nameof(LordsNeedsTutorIssueBehavior.ConditionsHold))]
    [HarmonyPostfix]
    private static void GiverPostfix(Hero issueGiver, ref bool __result) => SuitablePostfix(issueGiver, ref __result);
}

[HarmonyPatch(typeof(Issue), nameof(Issue.IssueStayAliveConditions))]
internal class TutorPlayerClanStayAlivePatch
{
    private static void Postfix(Issue __instance, ref bool __result)
    {
        if (__result && ContainerProvider.TryResolve<IPlayerManager>(out var players) &&
            (players.Contains(__instance.IssueOwner.Clan) || players.Contains(__instance._youngHero.Clan))) __result = false;
    }
}
