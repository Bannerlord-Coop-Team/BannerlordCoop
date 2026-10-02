using Common;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace GameInterface.Services.Issues.Patches;

using Issue = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;
using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

[HarmonyPatch]
internal static class GangLeaderWeaponsJournalOwnerPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(JournalLogsCampaignBehavior), "OnQuestStarted");
        yield return AccessTools.Method(typeof(JournalLogsCampaignBehavior), "OnQuestCompleted");
        yield return AccessTools.Method(typeof(JournalLogsCampaignBehavior), "OnQuestLogAdded");
    }

    [HarmonyPrefix]
    private static bool Prefix(QuestBase quest)
    {
        if (quest is not Quest weapons) return true;
        if (ModInformation.IsServer) return false;
        // Issue finalization clears ownership before the journal completion callback runs.
        if (GangLeaderWeaponsActionScope.Contains(weapons)) return GangLeaderWeaponsActionScope.IsLocalOwner;
        return ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) && ownership.IsLocalPeerOwner(quest.QuestGiver);
    }
}

[HarmonyPatch(typeof(QuestsVM), nameof(QuestsVM.RefreshValues))]
internal static class GangLeaderWeaponsJournalListPatch
{
    [HarmonyPrefix]
    private static void Prefix(QuestsVM __instance)
    {
        if (ModInformation.IsServer || !ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership)) return;
        foreach (var item in __instance.ActiveQuestsList.ToArray())
        {
            var giver = item.Quest is Quest quest ? quest.QuestGiver : (item.Issue as Issue)?.IssueOwner;
            if (giver != null && !ownership.IsLocalPeerOwner(giver)) __instance.ActiveQuestsList.Remove(item);
        }
        if (__instance.SelectedQuest != null && !__instance.ActiveQuestsList.Contains(__instance.SelectedQuest) &&
            !__instance.OldQuestsList.Contains(__instance.SelectedQuest))
        {
            var selected = __instance.ActiveQuestsList.FirstOrDefault() ?? __instance.OldQuestsList.FirstOrDefault();
            if (selected != null) __instance.SetSelectedItem(selected);
            else
            {
                __instance.SelectedQuest.IsSelected = false;
                __instance.SelectedQuest = null;
                __instance.CurrentQuestStages.Clear();
                __instance.CurrentQuestGiverHero = new HeroVM(null);
                __instance.CurrentQuestTitle = "";
                __instance.IsCurrentQuestGiverHeroHidden = true;
                __instance._viewDataTracker.SetQuestSelection(null);
            }
        }
        __instance.IsThereAnyQuest = __instance.ActiveQuestsList.Count != 0 || __instance.OldQuestsList.Count != 0;
    }
}
