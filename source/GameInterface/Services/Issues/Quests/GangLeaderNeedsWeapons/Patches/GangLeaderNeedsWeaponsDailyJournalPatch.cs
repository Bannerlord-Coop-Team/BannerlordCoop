using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons.Patches;

/// <summary>
/// A machine that did not start the mission only gets its state, no log entries. Vanilla's daily tick moves the
/// return days of the first entry of every running mission, so those machines get a quiet placeholder for it.
/// </summary>
[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal class GangLeaderNeedsWeaponsDailyJournalPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueManager __instance)
    {
        foreach (var entry in __instance.Issues)
        {
            if (entry.Value is Issue issue && issue.IsSolvingWithAlternative && issue.JournalEntries.Count == 0)
            {
                // Straight into the list, announcing it would put it in front of a player who sent nobody
                issue._journalEntries.Add(new JournalLog(
                    CampaignTime.Now,
                    new TextObject(string.Empty),
                    new TextObject("{=VFO7rMzK}Return Days"),
                    0,
                    issue.AlternativeSolutionBaseDurationInDaysInternal));
            }
        }
    }
}
