using GameInterface.Policies;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Optional defensive hardening (design doc §3.1, low priority - built anyway since this quest's Instance-
/// adjacent code was already being touched for the accept-time spawn-authority fix). Same shape as
/// <c>BettingFraudInstanceResolutionPatch.cs</c>: vanilla's private static <c>Instance</c> getter caches a
/// single <c>_cachedQuest</c> field and otherwise falls back to "first ongoing quest of this type found in
/// <c>QuestManager.Quests</c>" - <c>engage_poachers_consequence</c>/<c>army_of_poachers_village_on_init</c> both
/// call it.
///
/// Not a cross-peer exploit - per doc/GroupA_HostileMobilePartySync_Design_v3.md §5, a non-owner's own mirrored
/// quest never calls <c>StartQuest()</c>, so <c>IsOngoing</c> stays false and it's never a candidate in the
/// "first ongoing" scan on that peer's own machine. The real (minor) risk this closes: the SAME local player
/// running two concurrent Merchant Army of Poachers quests (from two different merchant NPCs) picking the wrong
/// one - an existing vanilla ambiguity, not introduced by this mod.
///
/// Unlike Betting Fraud, this quest has a location field (<c>_questVillage</c>) that can disambiguate directly
/// instead of falling back to ownership: resolves by matching <c>_questVillage</c>'s settlement against the
/// local player's current settlement/encounter context (the same field <c>army_of_poachers_village_on_init</c>/
/// <c>HourlyTick</c> already key off of), falling back to first-ongoing to preserve vanilla's original
/// permissiveness if nothing disambiguates.
/// </summary>
[HarmonyPatch(typeof(MerchantArmyOfPoachersIssueBehavior), "Instance", MethodType.Getter)]
internal class MerchantArmyOfPoachersInstanceResolutionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var currentSettlement = Settlement.CurrentSettlement ?? PlayerEncounter.EncounterSettlement;
        MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest fallback = null;

        foreach (var quest in Campaign.Current.QuestManager.Quests)
        {
            if (quest is not MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest candidate || !candidate.IsOngoing)
                continue;

            fallback ??= candidate;
            if (currentSettlement != null && candidate._questVillage?.Settlement == currentSettlement)
            {
                __result = candidate;
                return false;
            }
        }

        __result = fallback;
        return false;
    }
}
