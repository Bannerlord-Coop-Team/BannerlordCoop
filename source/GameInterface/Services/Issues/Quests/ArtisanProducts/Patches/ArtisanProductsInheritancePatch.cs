using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnIssueOwnerChanged))]
internal class ArtisanProductsInheritancePatch
{
    [HarmonyPostfix]
    private static void After(IssueBase issue, Hero oldOwner)
    {
        if (!(issue is Issue) || issue.IssueQuest == null) return;
        var quest = issue.IssueQuest;
        quest.QuestGiver = issue.IssueOwner;
        var manager = Campaign.Current.QuestManager;
        if (!manager.Quests.Contains(quest)) return;
        if (manager.TrackedObjects.TryGetValue(oldOwner, out var oldQuests) && oldQuests.Contains(quest))
            manager.RemoveTrackedObjectForQuest(oldOwner, quest);
        if (!manager.TrackedObjects.TryGetValue(issue.IssueOwner, out var newQuests) || !newQuests.Contains(quest))
            manager.AddTrackedObjectForQuest(issue.IssueOwner, quest);
    }
}
