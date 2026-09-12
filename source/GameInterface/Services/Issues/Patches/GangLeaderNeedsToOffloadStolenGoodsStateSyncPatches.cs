using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Issue = GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;
using Quest = GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.RegisterEvents))]
internal class GangLeaderNeedsToOffloadStolenGoodsConversationStateSyncPatches
{
    private static readonly ConditionalWeakTable<IssuesCampaignBehavior, object> listenerRegistered = new();

    [HarmonyPostfix]
    private static void RegisterEventsPostfix(IssuesCampaignBehavior __instance)
    {
        if (listenerRegistered.TryGetValue(__instance, out _)) return;
        listenerRegistered.Add(__instance, null);

        CampaignEvents.ConversationEnded.AddNonSerializedListener(__instance, OnConversationEnded);
    }

    private static void OnConversationEnded(IEnumerable<CharacterObject> characters)
    {
        if (Campaign.Current?.IssueManager == null) return;

        var snapshot = new List<KeyValuePair<Hero, IssueBase>>(Campaign.Current.IssueManager.Issues);
        foreach (var kvp in snapshot)
        {
            if (kvp.Value is not Issue issue) continue;
            if (issue.IssueQuest is not Quest quest) continue;

            GangLeaderNeedsToOffloadStolenGoodsQuestType.SyncStateToServer(kvp.Key, quest);
        }
    }
}
