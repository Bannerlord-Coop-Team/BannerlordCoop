using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// CONFIRMED gap (matching the task's own "known facts" bullet, verified directly against decompiled source):
/// <c>GangLeaderNeedsWeaponsIssueQuest.SetDialogs()</c>'s <c>quest_discuss</c> dialogue gates its "Here is your
/// cargo" option on <c>CheckIfPlayerHasEnoughRequestedWeapons()</c>, which recomputes
/// <c>_collectedItemAmount</c> from whichever LOCAL machine's own <c>PartyBase.MainParty.ItemRoster</c> is
/// currently running this dialogue, with no ownership check anywhere in the chain - and <c>quest_discuss</c>'s
/// own top-level Condition only checks <c>Hero.OneToOneConversationHero == base.QuestGiver</c> (the shared
/// quest-giver NPC, not owner-specific). Any connected peer carrying &gt;= the requested amount can complete
/// and collect someone else's already-accepted quest. Gating this one condition method is sufficient -
/// <c>PlayerSuccessfullyDeliveredWeapons</c> is only ever reached through this option's Consequence, no
/// proactive/out-of-band path exists for this quest.
///
/// THREE further gaps, independently found this pass (not in the task's "known facts", same shape as this
/// session's other independently-found gaps - see e.g. <c>CaravanAmbushQuestOwnershipGatePatch</c>): the guard
/// dialogue (<c>GetGuardDialogFlow()</c>, registered UNCONDITIONALLY from the ctor's <c>SetDialogs()</c> call on
/// EVERY peer's own mirror, not just the accepter's) has no ownership check either, and - now that
/// <see cref="GangLeaderNeedsWeaponsGuardsPartySpawnGatePatch"/> correctly force-mirrors the real, globally-
/// visible <c>_guardsParty</c> onto every peer - any connected player can physically walk up to and talk to it.
/// Its Condition methods (<c>DialogStartCondition</c>/<c>persuasion_start_with_guards_on_condition</c>) are
/// already null-safe (<c>this._guardsParty != null &amp;&amp; ...</c>, confirmed by direct read - unlike Caravan
/// Ambush's own independently-found null-deref bug, this quest has none here), so the fix is the same "gate the
/// actual mutation" principle applied to its three convergent leaf methods:
///
/// - <c>PlayerDodgedGuards()</c> (destroys the shared <c>_guardsParty</c> + sets <c>_playerDodgedGuards</c>) -
///   reached from THREE places: <c>PlayerDodgeGuardsLowCrimeRating</c> (big-party intimidation), the persuasion
///   minigame's success consequence, AND (for free) <c>OnGameMenuOpened</c>'s own battle-WIN branch - already
///   safe on its own since <see cref="GangLeaderNeedsWeaponsBattleStartApprovalPatches"/> ensures
///   <c>_checkForBattleResult</c>/a real involving <c>PlayerEncounter.Battle</c> only ever legitimately exist on
///   the genuine owner's own machine, but gated here too for defense-in-depth per this session's established
///   "gate the mutation, not each registration/condition site" rule.
/// - <c>DeleteAllWeaponsFromPlayer()</c> (transfers weapons out of the TALKER's own <c>PartyBase.MainParty.ItemRoster</c>
///   into <c>_weaponsThatGuardTook</c>) - reached from TWO "hand over our weapons" dialogue paths.
/// - <c>PlayerBribeGuard()</c> - spends the TALKER's own gold (<c>GiveGoldAction.ApplyBetweenCharacters</c>)
///   BEFORE calling <c>PlayerDodgedGuards()</c>. Gated directly (not just relying on its own tail call being
///   gated) so a non-owner never loses gold for nothing - gating only the tail would leave the wasted gold-spend
///   unprevented, a worse outcome than doing nothing.
/// </summary>
[HarmonyPatch(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest))]
internal class GangLeaderNeedsWeaponsQuestOwnershipGatePatch
{
    [HarmonyPatch("CheckIfPlayerHasEnoughRequestedWeapons")]
    [HarmonyPrefix]
    private static bool CheckIfPlayerHasEnoughRequestedWeaponsPrefix(
        GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest __instance, ref bool __result)
    {
        if (VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver)) return true;

        __result = false;
        return false; // skip the original - non-owners never see the option become selectable
    }

    [HarmonyPatch("PlayerDodgedGuards")]
    [HarmonyPrefix]
    private static bool PlayerDodgedGuardsPrefix(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("DeleteAllWeaponsFromPlayer")]
    [HarmonyPrefix]
    private static bool DeleteAllWeaponsFromPlayerPrefix(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("PlayerBribeGuard")]
    [HarmonyPrefix]
    private static bool PlayerBribeGuardPrefix(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
