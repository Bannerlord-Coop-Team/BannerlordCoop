using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures the exact moment either of the two never-reset, non-<c>[SaveableField]</c> booleans on
/// <see cref="LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest"/> becomes true, on whichever peer's own
/// invocation is genuinely the recorded owner's (see
/// <see cref="Patches.LordsNeedsTutorOwnershipGatePatches"/>, which gates BOTH real methods below to the
/// recorded owner only - so a non-owner's own copy of either method never sets its field at all, and this
/// patch's job is purely to broadcast the owner's genuine transition to every other peer's shadow table).
///
/// <c>OnHeroesMarried</c> needs a PREFIX, not a postfix: its own body sets <c>_doNotForceYoungHeroOutFromClan</c>
/// THEN calls <c>CompleteQuestWithCancel(...)</c> INLINE, synchronously, before returning - which itself
/// cascades all the way through <c>IssueFinalized</c>'s own network broadcast
/// (<see cref="IssueFinalizedPatches"/>). A postfix here would only run AFTER that whole cascade (and its
/// broadcast) already happened, arriving at other peers too late to correct their own mirrored
/// <c>OnFinalize</c> replay. A prefix - replicating the exact same hero-identity condition vanilla's own body
/// checks, so it is provably a harmless duplicate rather than an independent guess - runs (and can therefore
/// publish) strictly BEFORE that cascade even starts, guaranteeing this broadcast is queued (and, on a reliable
/// ordered channel, delivered) ahead of the finalize broadcast the SAME call produces.
///
/// <c>OnTimedOut</c>, by contrast, is safe with a POSTFIX: its own body never itself calls any
/// <c>CompleteQuestWithXxx</c> (confirmed against the decompiled source - it only logs, adjusts
/// <c>RelationshipChangeWithQuestGiver</c>, sets the flag, and maybe opens a conversation); the actual
/// timeout-completion cascade is triggered separately, by the CALLER, only after this override (postfix
/// included) has fully returned - so a postfix here still runs strictly before that cascade begins.
/// </summary>
[HarmonyPatch]
internal class LordsNeedsTutorQuestFlagCapturePatch
{
    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "OnHeroesMarried")]
    [HarmonyPrefix]
    private static void OnHeroesMarriedPrefix(
        LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance, Hero hero1, Hero hero2)
    {
        // Exact same condition as the real vanilla body - see the type doc comment for why duplicating it here
        // (rather than reading the field after the fact) is required for correct broadcast ordering.
        bool willSetFlag = (hero1 == Hero.MainHero && hero2 == __instance._youngHero) ||
                            (hero2 == Hero.MainHero && hero1 == __instance._youngHero);
        if (!willSetFlag) return;

        Publish(__instance.QuestGiver, LordsNeedsTutorQuestFlag.DoNotForceYoungHeroOutFromClan);
    }

    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "OnTimedOut")]
    [HarmonyPostfix]
    private static void OnTimedOutPostfix(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance)
    {
        if (!__instance._showQuestFailedConversation) return;

        Publish(__instance.QuestGiver, LordsNeedsTutorQuestFlag.ShowQuestFailedConversation);
    }

    private static void Publish(Hero questGiver, LordsNeedsTutorQuestFlag flag)
    {
        if (questGiver == null) return;

        // Record locally too - the owner's own machine needs its shadow-table entry populated for its OWN
        // future save/reload, exactly like every other registry in this family.
        LordsNeedsTutorQuestFlags.SetFlag(questGiver, flag);

        MessageBroker.Instance.Publish(questGiver, new LordsNeedsTutorQuestFlagTriggered(questGiver, flag));
    }
}
