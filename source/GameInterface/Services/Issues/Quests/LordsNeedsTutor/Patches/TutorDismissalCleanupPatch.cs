using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest;
using Issue = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue;

namespace GameInterface.Services.Issues.Quests.LordsNeedsTutor.Patches;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal class TutorDismissalCleanupPatch
{
    private static void Prefix(IssueBase __instance)
    {
        if (ModInformation.IsClient || __instance is not Issue || __instance.IssueQuest is not Quest quest ||
            !quest.IsOngoing || quest._startQuestLog == null) return;
        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var resolver)) return;
        // At server startup the ownership registry is empty, but the pupil still names the owning clan.
        if (!resolver.TryResolveOwnerHero(__instance, out var owner)) owner = quest._youngHero.CompanionOf?.Leader;
        if (owner == null) return;
        using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
        {
            quest.FinalizeQuest();
            quest.AfterFinalize();
        }
    }
}
