using GameInterface.Policies;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug A fix (doc/EscortMerchantCaravan_Design_v2.md §2): <c>EscortMerchantCaravanIssueQuest</c>'s constructor
/// runs via <c>GenerateIssueQuest()</c> on EVERY peer, unconditionally, and calls <c>SetDialogs()</c> before
/// ownership is even knowable - <c>SetDialogs()</c> registers 5 global conversation dialogue flows, 3 of which
/// (<c>GetCaravanPartyDialogFlow</c>/<c>GetCaravanGreetingDialogFlow</c>/<c>GetCaravanFarewellDialogFlow</c>)
/// share the SAME condition delegate, <c>caravan_talk_on_condition()</c>, which dereferences
/// <c>_questCaravanMobileParty.MemberRoster</c> as its very first expression with no null check.
/// <c>_questCaravanMobileParty</c> is only ever set by <c>SpawnCaravan()</c>, itself reachable only through the
/// real accepter's own live <c>QuestAcceptedConsequences()</c> - permanently null on every non-owner mirror.
/// Because these are GLOBAL conversation flows keyed to shared dialogue-state tokens ("start"/
/// "escort_caravan_talk"), this NRE is reachable the moment ANY peer's conversation system evaluates either
/// state while ANY non-owner mirror of this quest type exists anywhere in the campaign - not rare, routine.
///
/// Two OTHER flows registered by the same <c>SetDialogs()</c> call
/// (<c>GetCaravanTradeDialogFlow</c>/<c>GetCaravanLootDialogFlow</c>) use DIFFERENT condition delegates
/// (<c>caravan_buy_products_on_condition</c>/<c>caravan_loot_on_condition</c>) that only ever do
/// <c>MobileParty.ConversationParty == _questCaravanMobileParty</c> reference comparisons - confirmed safe
/// against a null field (<c>MobileParty</c> is <c>sealed</c> with no <c>operator ==</c>/<c>!=</c> override, so
/// comparing against null never dereferences it) - deliberately left untouched, narrowest possible fix.
///
/// A single Harmony prefix on the one unsafe method covers BOTH registration sites
/// (<c>SetDialogs()</c>, called from both the ctor AND <c>InitializeQuestOnGameLoad()</c>) automatically, since
/// it patches the shared delegate itself rather than either call site.
/// </summary>
[HarmonyPatch(typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest), "caravan_talk_on_condition")]
internal class EscortMerchantCaravanCaravanTalkConditionNullGuardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance, ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var questCaravanMobileParty = GetQuestCaravanMobileParty(__instance);
        if (questCaravanMobileParty == null)
        {
            __result = false; // no caravan yet on this mirror - this dialogue option can never legitimately apply
            return false; // skip the original, avoid the NRE
        }

        return true; // caravan exists (real owner, or a legitimately-resolved joiner) - run vanilla logic unmodified
    }

    private static MobileParty GetQuestCaravanMobileParty(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest instance) =>
        AccessTools.Field(instance.GetType(), "_questCaravanMobileParty").GetValue(instance) as MobileParty;
}
