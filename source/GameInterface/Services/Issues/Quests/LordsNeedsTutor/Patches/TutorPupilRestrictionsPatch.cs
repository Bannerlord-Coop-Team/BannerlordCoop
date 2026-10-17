using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using Common;
using Quest = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest;
using Issue = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue;

namespace GameInterface.Services.Issues.Quests.LordsNeedsTutor.Patches;

[HarmonyPatch]
internal class TutorPupilRestrictionsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(QuestManager), nameof(QuestManager.CanHaveCampaignIssues));
        yield return AccessTools.Method(typeof(QuestManager), nameof(QuestManager.CanHeroEquipmentBeChanged));
        yield return AccessTools.Method(typeof(QuestManager), nameof(QuestManager.CanHeroLeadParty));
        yield return AccessTools.Method(typeof(QuestManager), nameof(QuestManager.CanHeroMarry));
        yield return AccessTools.Method(typeof(QuestManager), nameof(QuestManager.CanBeGovernorOrHavePartyRole));
    }

    private static void Postfix(Hero hero, ref bool result)
    {
        if (result && Campaign.Current.IssueManager.Issues.Values.OfType<Issue>()
            .Any(issue => issue.IsSolvingWithQuest && issue._youngHero == hero)) result = false;
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.OnIssueUpdated))]
internal class TutorIssueWorldConsequencesPatch
{
    private static bool Prefix(IssueBase issue) => issue is not Issue || ModInformation.IsServer;
}
