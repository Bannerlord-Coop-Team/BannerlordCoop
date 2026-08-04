using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation) for <see cref="LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest"/> - independently traced
/// against the decompiled source method-by-method (task item 4: "don't assume any listener needs a gate without
/// tracing it yourself"), not trusted from the task's own framing.
///
/// This quest's completion/dialogue surface differs structurally from every prior quest type this session: its
/// turn-in conversation is with <c>_youngHero</c> - a COMPANION physically added to the recorded owner's own
/// party (<c>QuestAcceptedConsequences</c>'s <c>MobileParty.MainParty.MemberRoster.AddToCounts</c>, confirmed
/// per task's own "no redirect needed" note - each peer's own <c>MainParty</c> is a distinct object, and this
/// consequence only ever runs on the genuine accepter's own machine), not a settlement Notable every peer can
/// walk up to. <c>Hero.OneToOneConversationHero == _youngHero</c> (the shared condition every
/// <c>_youngHero</c>-conversation dialogue flow checks) is therefore only ever reachable from the party/companion
/// screen of whichever peer's OWN party actually contains <c>_youngHero</c> - structurally, that can only be the
/// recorded owner. <c>QuestCompletedWithSuccessAfterConversation</c> is STILL gated below anyway, as free
/// defense-in-depth (matching this session's established "gate it anyway even when provably inert" pattern for
/// e.g. Revenue Farming's <c>OnMapEventEnded</c>/<c>OnPlayerDeserted</c>) rather than leaving a completion path
/// entirely unguarded on an inference this thorough but not runtime-provable in this harness.
///
/// REGISTEREVENTS LISTENERS - traced each of the 13 individually against the decompiled source:
///
/// - <c>OnHeroKilled</c>: <c>victim == _youngHero</c> - GLOBAL, not <c>Hero.MainHero</c>-relative (the SAME
///   shared <c>_youngHero</c> identity, so this condition is IDENTICALLY true on every peer's own listener for
///   the SAME kill event) - unconditionally calls <c>CompleteQuestWithFail()</c>. Every peer would otherwise
///   independently cascade to <c>IssueFinalized</c> for the same kill - a real N-way-redundant-broadcast risk,
///   the same "correctly triggers redundantly rather than wrongly" shape <c>RevenueFarmingOwnershipGatePatches</c>
///   already documents for its own settlement-owner-changed listener. LOAD-BEARING - gated.
/// - <c>OnClanLeaderChanged</c>: <c>newLeader == _youngHero</c> - GLOBAL, same N-way redundant shape. LOAD-BEARING
///   - gated.
/// - <c>OnCompanionRemoved</c>: <c>companion == _youngHero</c> - GLOBAL, same N-way redundant shape (also
///   interacts with the just-fixed <c>RemoveCompanionAction</c> client-forwarding: once that forwards correctly,
///   every peer's own listener for the SAME removal event needs this same defense). LOAD-BEARING - gated.
/// - <c>OnWarDeclared</c> -&gt; <c>QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest</c>: checks
///   <c>QuestGiver.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)</c> - <c>Hero.MainHero</c>-relative, the same
///   shape <c>RevenueFarmingOwnershipGatePatches</c> already documents as load-bearing for its own type.
///   LOAD-BEARING - gated.
/// - <c>OnClanChangedKingdom</c>: same <c>Hero.MainHero</c>-relative condition, same
///   <c>CompleteQuestWithCancel</c> call. LOAD-BEARING - gated.
/// - <c>OnMapEventStarted</c> -&gt; <c>QuestHelper.CheckMinorMajorCoercion</c>: checks
///   <c>attackerParty == PartyBase.MainParty</c> - <c>Hero.MainHero</c>-relative via the local process's own
///   <c>MainParty</c>. LOAD-BEARING - gated.
/// - <c>OnHeroesMarried</c>: <c>(hero1 == Hero.MainHero &amp;&amp; hero2 == _youngHero) || (hero2 ==
///   Hero.MainHero &amp;&amp; hero1 == _youngHero)</c> - <c>Hero.MainHero</c>-relative, AND (since <c>_youngHero</c>
///   only ever resides in the recorded owner's own party) realistically only ever true for the owner's own
///   machine in the first place. Also sets <c>_doNotForceYoungHeroOutFromClan</c> and calls
///   <c>CompleteQuestWithCancel</c> - LOAD-BEARING (N-way redundant risk if ever reached by more than one peer) -
///   gated. See <see cref="LordsNeedsTutorQuestFlagCapturePatch"/> for how the flag itself still gets mirrored to
///   every OTHER peer despite this gate.
/// - <c>OnTimedOut</c>: unlike Revenue Farming's own barebones "AddLog and nothing else" (deliberately left
///   ungated there), THIS quest's <c>OnTimedOut</c> ALSO sets <c>RelationshipChangeWithQuestGiver = -10</c> - a
///   property read once by the base due-time-completion machinery to apply a real relation mutation to the
///   shared <c>QuestGiver</c> Hero - reached via the SAME "ambient, per-peer-independent due-time tick, not
///   host/client-gated anywhere else" mechanism <c>RevenueFarmingOwnershipGatePatches.OnBeforeTimedOutPrefix</c>'s
///   doc comment documents (every peer's own mirrored quest independently reaches the SAME due time and would
///   otherwise double/triple-apply this). LOAD-BEARING - gated. See
///   <see cref="LordsNeedsTutorQuestFlagCapturePatch"/> for how <c>_showQuestFailedConversation</c> still gets
///   mirrored to every OTHER peer despite this gate.
/// - <c>OnPrisonerTaken</c>: <c>prisoner == Hero.MainHero &amp;&amp; ... &amp;&amp;
///   MobileParty.MainParty...Troops.FindIndexOfCharacter(_youngHero.CharacterObject).IsValid</c> - structurally
///   requires <c>_youngHero</c> to be a member of THIS PROCESS's own <c>MainParty</c>, which (same reasoning as
///   the type doc comment above) can only be the recorded owner. NOT load-bearing (provably inert on a
///   non-owner), gated anyway as free, zero-risk defense-in-depth.
/// - <c>OnNewCompanionAdded</c>/<c>OnPerkReset</c>: both unconditionally (or, for the latter, only when
///   <c>hero == Hero.MainHero</c>) call the private <c>CheckCompanionLimit()</c>, gated directly below instead of
///   gating these two listeners separately - a single choke point for all three call sites.
/// - <c>CheckCompanionLimit</c> (also called from <c>OnGameLoadFinished</c>): reads
///   <c>Clan.PlayerClan.Companions.Count</c>/<c>CompanionLimit</c> - GLOBAL/shared-clan state, identical on every
///   peer - and calls <c>CompleteQuestWithCancel()</c> when over limit. Same N-way redundant risk as the GLOBAL
///   listeners above. LOAD-BEARING - gated.
/// - <c>OnSettlementEntered</c>/<c>OnGameLoadFinished</c>'s OWN <c>SpawnYoungHeroInLordsHall()</c> call: purely a
///   local, per-peer VISUAL scene-population method (spawns a visible agent in the CURRENT peer's own rendered
///   "lordshall" location) - not a shared-state mutation, and legitimately wanted on every peer whose own party
///   happens to enter/load into that settlement while <c>_youngHero</c> is there. NOT gated (deliberately - no
///   "correctly triggers redundantly rather than wrongly" concern applies to local-only scene population).
///
/// <c>QuestCompletedWithSuccessAfterConversation</c> is the sole real completion leaf method (calls
/// <c>CompleteQuestWithSuccess()</c> directly, inline - no chained-external-Consequence gap like Artisan
/// Overpriced Goods) - gated below as described in the type doc comment above.
/// </summary>
[HarmonyPatch]
internal class LordsNeedsTutorOwnershipGatePatches
{
    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "OnHeroKilled")]
    [HarmonyPrefix]
    private static bool OnHeroKilledPrefix(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "OnClanLeaderChanged")]
    [HarmonyPrefix]
    private static bool OnClanLeaderChangedPrefix(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "OnCompanionRemoved")]
    [HarmonyPrefix]
    private static bool OnCompanionRemovedPrefix(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "OnWarDeclared")]
    [HarmonyPrefix]
    private static bool OnWarDeclaredPrefix(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "OnClanChangedKingdom")]
    [HarmonyPrefix]
    private static bool OnClanChangedKingdomPrefix(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "OnMapEventStarted")]
    [HarmonyPrefix]
    private static bool OnMapEventStartedPrefix(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "OnHeroesMarried")]
    [HarmonyPrefix]
    private static bool OnHeroesMarriedPrefix(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "OnTimedOut")]
    [HarmonyPrefix]
    private static bool OnTimedOutPrefix(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    // Defense-in-depth only - see the type doc comment for why this is provably inert on a non-owner mirror
    // (structurally requires _youngHero to be a member of THIS process's own MainParty).
    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "OnPrisonerTaken")]
    [HarmonyPrefix]
    private static bool OnPrisonerTakenPrefix(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "CheckCompanionLimit")]
    [HarmonyPrefix]
    private static bool CheckCompanionLimitPrefix(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    // Defense-in-depth only (see the type doc comment) - not a tautological stub, since a non-owner can
    // structurally never reach Hero.OneToOneConversationHero == _youngHero at all to trigger this in the first
    // place, but gated anyway matching this session's established "gate it anyway" pattern.
    [HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest), "QuestCompletedWithSuccessAfterConversation")]
    [HarmonyPrefix]
    private static bool QuestCompletedWithSuccessAfterConversationPrefix(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
