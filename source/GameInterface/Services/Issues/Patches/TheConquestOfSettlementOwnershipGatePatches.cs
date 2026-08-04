using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug 2 fix - <c>TheConquestOfSettlementIssueQuest.OnSettlementOwnerChanged</c>'s <c>ByBarter</c> branch.
///
/// This listener DOES correctly reach every peer's own copy of it (unlike Bug 1's <c>OnSiegeCompleted</c>):
/// <see cref="Handlers.SettlementOwnershipHandler"/>'s <c>NetworkChangeSettlementOwnership</c> handling
/// re-invokes <c>CampaignEventDispatcher.Instance.OnSettlementOwnerChanged</c> directly on every peer's own
/// process (no <c>ModInformation.IsClient/IsServer</c> gate on who processes it), so whichever machine
/// genuinely has this quest's listener wired (the real accepter's own machine, wherever it is) receives it.
/// The bug is purely in WHAT the <c>ByBarter</c> success branch compares: <c>newOwner == Hero.MainHero</c> reads
/// "did the settlement's new owner become THIS PROCESS'S own locally-controlled avatar" - correct only on the
/// real owner's own machine, by construction, but this same listener can ALSO be reached (or could become
/// reachable, e.g. once any future fix wires it more broadly - see Bug 1's own doc comment on why that gap
/// exists at all) on a peer that is NOT this quest's owner. On such a peer, an unrelated player B who
/// independently barters for <c>_targetSettlement</c> (nothing to do with this quest) has
/// <c>newOwner == Hero.MainHero</c> evaluate true from THEIR OWN perspective too, wrongly firing
/// <c>QuestSuccess</c>, paying B instead of the real owner, and force-finalizing the real owner's quest out
/// from under them via the shared <see cref="IssueFinalizedPatches"/> broadcast.
///
/// Fixed by replacing the <c>Hero.MainHero</c> comparison with the quest's real recorded owner identity, via
/// <see cref="TheConquestOfSettlementIssueOwnershipResolver"/> (the same
/// <see cref="VillageNeedsToolsIssueOwnership"/> mechanism every other ownership fix in this codebase already
/// uses) - much simpler than Bug 1's fix since no new event-delivery mechanism is needed here, just correcting
/// which Hero gets compared against <c>newOwner</c>. The "someone else took it, cancel" else-branch is
/// deliberately left untouched (task-confirmed out of scope for this bug, and it has no comparable exploit
/// shape - it only ever cancels the LOCAL listener's own quest, never pays anyone).
/// </summary>
[HarmonyPatch(typeof(TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest), "OnSettlementOwnerChanged")]
internal class TheConquestOfSettlementSettlementOwnerChangedPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest __instance,
        Settlement settlement,
        bool openToClaim,
        Hero newOwner,
        Hero oldOwner,
        Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        if (!__instance.IsOngoing || settlement != __instance._targetSettlement) return false;

        if (detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByBarter)
        {
            // Not the buggy branch - let the real method run its own "taken by another faction" logic
            // unmodified. Its own IsOngoing/settlement guard will pass again harmlessly.
            return true;
        }

        if (TheConquestOfSettlementIssueOwnershipResolver.TryResolveOwner(__instance.QuestGiver, out var ownerHero, out _)
            && ReferenceEquals(newOwner, ownerHero))
        {
            __instance.QuestSuccess(2);
        }

        return false;
    }
}
