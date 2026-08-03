using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Fixes vanilla's campaign-wide single-slot cache/first-found <c>Instance</c> resolution (see that private
/// static getter's own body: <c>_cachedQuest</c>/"first BettingFraudQuest found in QuestManager.Quests") that
/// <c>game_menu_tournament_join_current_game_on_consequence</c> relies on - the ONLY call site of
/// <c>Instance</c> in the whole behavior class - to find which <c>BettingFraudQuest</c> the player's "Join"
/// click at <c>menu_town_tournament_join_betting_fraud</c> belongs to.
///
/// Unlike Lord Needs Garrison Troops, this quest type has no <c>_settlement</c> field to key off of - the
/// quest never stores one, it just reacts to whatever <see cref="TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement"/>
/// happens to be at the time. What DOES uniquely identify "my own quest" here: <c>menu_town_tournament_join_betting_fraud</c>
/// is only EVER reached via a specific quest instance's own <c>OnGameMenuOpened</c> listener redirecting the
/// "menu_town_tournament_join" menu - and that listener is subscribed by <c>RegisterEvents()</c>, which vanilla
/// only ever calls from <c>QuestBase.StartQuest()</c>, which itself only runs inside the live
/// <c>OfferDialogFlowConsequence</c> dialogue consequence - i.e. only on the machine whose OWN conversation
/// genuinely accepted this quest (see <see cref="IBettingFraudIssueInterface"/>'s doc comment; confirmed
/// against <c>IssueBase.StartIssueWithQuest</c>/<c>IssueManager.StartIssueQuest</c>, which our own
/// replay/mirror sync path also calls, but which only constructs the Quest object - it never itself calls
/// <c>StartQuest()</c>). So on THIS client, whichever ongoing <c>BettingFraudQuest</c> this LOCAL peer actually
/// owns is always the one - and only one - quest capable of having redirected the player here; a mirrored copy
/// of some other player's concurrently-active Betting Fraud quest (same type, different gang-leader NPC -
/// ordinary coop play, not a rare edge case) never subscribes and could never have caused this menu to appear.
///
/// Fixed by resolving <c>Instance</c> to the ongoing quest whose <c>QuestGiver</c> this local peer owns
/// (<see cref="VillageNeedsToolsIssueOwnership.IsLocalPeerOwner"/> - already populated for this issue type by
/// <see cref="Handlers.BettingFraudIssueHandler"/>'s accept broadcast, the same registry the pre-existing
/// ownership-gate patches elsewhere in this issue family already rely on) instead of "first found"/cached. This
/// is purely local (no network round-trip needed): the ownership record was already synced at accept time, long
/// before the player could physically walk to a tournament town and click Join.
///
/// Left alone (runs the untouched vanilla getter) whenever <see cref="CallOriginalPolicy.IsOriginalAllowed"/>
/// is true, matching every other patch in this issue family.
/// </summary>
[HarmonyPatch(typeof(BettingFraudIssueBehavior), "Instance", MethodType.Getter)]
internal class BettingFraudInstanceResolutionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref BettingFraudIssueBehavior.BettingFraudQuest __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        __result = null;

        foreach (QuestBase quest in Campaign.Current.QuestManager.Quests)
        {
            if (quest is BettingFraudIssueBehavior.BettingFraudQuest candidate
                && candidate.IsOngoing
                && VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(candidate.QuestGiver))
            {
                __result = candidate;
                break;
            }
        }

        return false; // skip the original cache/first-found lookup entirely
    }
}
