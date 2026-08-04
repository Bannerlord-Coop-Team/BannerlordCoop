using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Persists <see cref="LordsNeedsTutorQuestFlags"/> in the existing <see cref="LordsNeedsTutorIssueBehavior"/>
/// save record (it already has a non-empty <c>SyncData</c> override syncing <c>_alreadyChosenHeroes</c>), mirrors
/// it back onto a live Quest instance's own private fields the moment either becomes known (network receipt AND
/// game-load rehydrate), and drains a hero's entry on genuine finalize - the exact same piggyback/rehydrate/drain
/// shape as <see cref="VillageNeedsToolsIssueOwnershipPersistencePatches"/>/
/// <see cref="Interfaces.VillageNeedsToolsIssueOwnership"/>.
/// </summary>
[HarmonyPatch]
internal class LordsNeedsTutorQuestFlagsPersistencePatches
{
    private const string SaveKey = "_coop_lords_needs_tutor_quest_flags";

    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior), nameof(LordsNeedsTutorIssueBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(IDataStore dataStore)
    {
        List<LordsNeedsTutorQuestFlagsSaveData> saveData = null;
        if (dataStore.IsSaving)
        {
            saveData = LordsNeedsTutorQuestFlags.Snapshot()
                .Select(e => new LordsNeedsTutorQuestFlagsSaveData(e.QuestGiver, e.DoNotForceYoungHeroOutFromClan, e.ShowQuestFailedConversation))
                .ToList();
        }

        dataStore.SyncData(SaveKey, ref saveData);
        if (!dataStore.IsLoading) return;

        LordsNeedsTutorQuestFlags.ClearAll();
        if (saveData == null) return;

        foreach (var entry in saveData)
        {
            if (entry?.QuestGiverHero == null) continue;
            LordsNeedsTutorQuestFlags.Restore(entry.QuestGiverHero, entry.DoNotForceYoungHeroOutFromClan, entry.ShowQuestFailedConversation);
        }
    }

    /// <summary>
    /// Rehydrate onto the live Quest instance's own private fields - covers BOTH an existing peer's own
    /// save/reload AND a late-joining peer materializing its Quest object fresh from the server's save-transfer
    /// snapshot (both reach <c>InitializeQuestOnGameLoad</c>, the real vanilla per-Quest-instance load hook,
    /// called AFTER every behavior's own <c>SyncData</c> above has already repopulated the registry).
    /// </summary>
    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "InitializeQuestOnGameLoad")]
    [HarmonyPostfix]
    private static void InitializeQuestOnGameLoadPostfix(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance)
    {
        if (!LordsNeedsTutorQuestFlags.TryGet(__instance.QuestGiver, out var doNotForce, out var showFailed)) return;

        if (doNotForce) __instance._doNotForceYoungHeroOutFromClan = true;
        if (showFailed) __instance._showQuestFailedConversation = true;
    }

    /// <summary>
    /// Drains this hero's entry on a genuine finalize (own, independent postfix on the same shared
    /// <see cref="IssueBase.IssueFinalized"/> method <see cref="IssueFinalizedPatches"/> already patches - Harmony
    /// runs multiple independent postfixes without conflict) so it can never leak into this hero's NEXT,
    /// unrelated issue - same leak concern <see cref="IssueFinalizedPatches"/> already documents for
    /// <see cref="Interfaces.VillageNeedsToolsIssueOwnership"/>. Runs unconditionally (genuine finalize OR a
    /// mirrored replay under <c>AllowedThread</c> - both reach <c>IssueFinalized</c> for real), filtered to this
    /// issue type only, kept in this file rather than added to the shared, generic <c>IssueFinalizedPatches</c>.
    /// </summary>
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPostfix]
    private static void IssueFinalizedPostfix(IssueBase __instance)
    {
        if (__instance is not LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue) return;

        LordsNeedsTutorQuestFlags.Clear(__instance.IssueOwner);
    }
}
