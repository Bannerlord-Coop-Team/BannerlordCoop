using GameInterface.Services.Issues.Messages;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Shadow-table mirror for the two never-reset, non-<c>[SaveableField]</c> booleans on
/// <see cref="LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest"/> - <c>_doNotForceYoungHeroOutFromClan</c>
/// (set true only inside the real <c>OnHeroesMarried</c>) and <c>_showQuestFailedConversation</c> (set true only
/// inside the real <c>OnTimedOut</c>). Neither field has ANY existing sync or persistence mechanism (confirmed
/// against the decompiled source - no <c>[SaveableField]</c> attribute on either), so on a non-owner mirror
/// (whose own copy of these methods is gated to the recorded owner only - see
/// <see cref="Patches.LordsNeedsTutorOwnershipGatePatches"/>) or after ANY save/reload (including a late-joining
/// peer materializing its own Quest object fresh from the server's save-transfer snapshot), these fields would
/// otherwise silently reset to their <c>false</c> default - even on the machine that legitimately set one of
/// them true moments earlier, once a save/reload intervenes. <c>OnFinalize</c> reads
/// <c>_doNotForceYoungHeroOutFromClan</c> to decide whether to force the (AutoSync'd) <c>_youngHero.Clan</c> back
/// to the quest giver's clan - since <c>OnFinalize</c> genuinely runs on EVERY peer (a mirrored finalize replay
/// calls the real <c>QuestBase.CompleteQuestWithXxx</c>, which reaches <c>OnFinalize</c>, under
/// <c>AllowedThread</c> - see <see cref="Patches.IssueFinalizedPatches"/>), an un-mirrored <c>false</c> on any
/// peer besides the one that set it true would incorrectly kick the young hero out of the (shared) player clan
/// on that peer's own replay, a real, observable <c>Hero.Clan</c> divergence.
///
/// Exact same shape as <see cref="VillageNeedsToolsIssueOwnership"/> (Hero-keyed by the issue's owning/quest-
/// giver Hero, since 1 issue per hero is enforced by <c>IssueManager.Issues</c> - every peer holds a DIFFERENT
/// <c>LordsNeedsTutorIssueQuest</c> instance for "the same logical issue", so they all converge on the SAME
/// owner-hero key), populated ONLY from the genuine owner's own captured flag-set (see
/// <see cref="Patches.LordsNeedsTutorQuestFlagCapturePatch"/>/<see cref="Handlers.LordsNeedsTutorIssueHandler"/>),
/// persisted alongside <c>LordsNeedsTutorIssueBehavior</c>'s own save record (see
/// <see cref="Patches.LordsNeedsTutorQuestFlagsPersistencePatches"/>), and consulted/rehydrated onto the live
/// Quest instance's own private fields both immediately on network receipt and on
/// <c>InitializeQuestOnGameLoad</c> (covering save/reload AND a late-joining peer alike) so every peer's mirror
/// converges on the same two flag values.
/// </summary>
internal static class LordsNeedsTutorQuestFlags
{
    private sealed class Entry
    {
        public bool DoNotForceYoungHeroOutFromClan;
        public bool ShowQuestFailedConversation;
    }

    private static readonly Dictionary<Hero, Entry> FlagsByQuestGiver = new();

    private static Entry GetOrAdd(Hero questGiver)
    {
        if (!FlagsByQuestGiver.TryGetValue(questGiver, out var entry))
        {
            entry = new Entry();
            FlagsByQuestGiver[questGiver] = entry;
        }
        return entry;
    }

    public static void SetFlag(Hero questGiver, LordsNeedsTutorQuestFlag flag)
    {
        if (questGiver == null) return;

        var entry = GetOrAdd(questGiver);
        switch (flag)
        {
            case LordsNeedsTutorQuestFlag.DoNotForceYoungHeroOutFromClan:
                entry.DoNotForceYoungHeroOutFromClan = true;
                break;
            case LordsNeedsTutorQuestFlag.ShowQuestFailedConversation:
                entry.ShowQuestFailedConversation = true;
                break;
        }
    }

    /// <summary>Restores a full pair straight from save data - used only while rehydrating from
    /// <see cref="Patches.LordsNeedsTutorQuestFlagsPersistencePatches"/>.</summary>
    public static void Restore(Hero questGiver, bool doNotForceYoungHeroOutFromClan, bool showQuestFailedConversation)
    {
        if (questGiver == null) return;
        if (!doNotForceYoungHeroOutFromClan && !showQuestFailedConversation) return;

        var entry = GetOrAdd(questGiver);
        entry.DoNotForceYoungHeroOutFromClan = doNotForceYoungHeroOutFromClan;
        entry.ShowQuestFailedConversation = showQuestFailedConversation;
    }

    public static bool TryGet(Hero questGiver, out bool doNotForceYoungHeroOutFromClan, out bool showQuestFailedConversation)
    {
        doNotForceYoungHeroOutFromClan = false;
        showQuestFailedConversation = false;
        if (questGiver == null || !FlagsByQuestGiver.TryGetValue(questGiver, out var entry)) return false;

        doNotForceYoungHeroOutFromClan = entry.DoNotForceYoungHeroOutFromClan;
        showQuestFailedConversation = entry.ShowQuestFailedConversation;
        return true;
    }

    /// <summary>Drains this hero's entry, if any, on a genuine finalize (see
    /// <see cref="Patches.LordsNeedsTutorQuestFlagsPersistencePatches"/>) so it can never leak into this hero's
    /// NEXT, unrelated Lord Needs a Tutor issue - same leak concern
    /// <see cref="VillageNeedsToolsIssueOwnership.Clear"/> already documents.</summary>
    public static void Clear(Hero questGiver)
    {
        if (questGiver == null) return;
        FlagsByQuestGiver.Remove(questGiver);
    }

    /// <summary>Wipes every entry - used when repopulating from save data on load.</summary>
    public static void ClearAll()
    {
        FlagsByQuestGiver.Clear();
    }

    /// <summary>Used by <see cref="Patches.LordsNeedsTutorQuestFlagsPersistencePatches"/> to save the registry
    /// alongside the behavior's own save record.</summary>
    public static IReadOnlyCollection<(Hero QuestGiver, bool DoNotForceYoungHeroOutFromClan, bool ShowQuestFailedConversation)> Snapshot()
    {
        var snapshot = new List<(Hero, bool, bool)>(FlagsByQuestGiver.Count);
        foreach (var kvp in FlagsByQuestGiver)
        {
            snapshot.Add((kvp.Key, kvp.Value.DoNotForceYoungHeroOutFromClan, kvp.Value.ShowQuestFailedConversation));
        }
        return snapshot;
    }
}
