using GameInterface.Services.Entity;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Records which connected player's <see cref="Player.ControllerId"/> genuinely accepted a given Village
/// Needs Tools issue - the missing "real ownership" concept both the dialogue-gate fix
/// (<see cref="Patches.VillageNeedsToolsIssueOwnershipGatePatches"/>) and the alternative-solution
/// completion fix (<see cref="Patches.VillageNeedsToolsAlternativeSolutionCompletionPatches"/>) need.
///
/// Keyed by the issue's owning/quest-giver <see cref="Hero"/> rather than by the
/// <c>VillageNeedsToolsIssue</c>/<c>VillageNeedsToolsIssueQuest</c> instance, because each peer holds a
/// DIFFERENT object instance representing "the same logical issue" (see
/// <see cref="VillageNeedsToolsIssueInterface"/>'s per-peer replicated construction) - they all share the
/// same owner Hero key, exactly like the existing
/// <see cref="Patches.IssueManagerQuestCompletedReasonCapture.PendingReasons"/> dictionary already relies on
/// (1 issue per hero, enforced by <c>IssueManager.Issues</c>).
///
/// Populated ONLY from the server's authoritative <c>NetworkVillageIssueQuestAccepted</c>/
/// <c>NetworkVillageIssueAlternativeAccepted</c> broadcast (see
/// <see cref="Handlers.VillageNeedsToolsIssueHandler"/>) - never optimistically set from a not-yet-confirmed
/// local accept, including on the accepting peer's own machine. That means there is a brief (one network
/// round-trip, milliseconds) window on the genuine accepter's own machine between their local accept
/// completing and their own confirmation arriving, during which <see cref="IsLocalPeerOwner"/> would
/// (correctly, fail-closed) read false for them too. This is a deliberate, accepted trade-off, not a bug:
/// the exploitable "turn the quest in" action is a separate, later conversation (the player has to walk
/// back up to the same NPC), not the same one that accepted it, so the window never matters in practice -
/// worst case is "try again a moment later."
/// </summary>
internal static class VillageNeedsToolsIssueOwnership
{
    private static readonly Dictionary<Hero, string> OwnerControllerIdByIssueGiver = new();

    public static void SetOwner(Hero issueGiver, string controllerId)
    {
        if (issueGiver == null || string.IsNullOrEmpty(controllerId)) return;

        OwnerControllerIdByIssueGiver[issueGiver] = controllerId;
    }

    /// <summary>
    /// Drains this hero's entry, if any, on a genuine finalize (see
    /// <see cref="Patches.IssueFinalizedPatches"/>) so it can never leak into this hero's NEXT, unrelated
    /// issue - exactly the same concern already handled for
    /// <see cref="Patches.IssueManagerQuestCompletedReasonCapture.PendingReasons"/>.
    /// </summary>
    public static void Clear(Hero issueGiver)
    {
        if (issueGiver == null) return;

        OwnerControllerIdByIssueGiver.Remove(issueGiver);
    }

    /// <summary>Wipes every entry - used when repopulating from save data on load.</summary>
    public static void ClearAll()
    {
        OwnerControllerIdByIssueGiver.Clear();
    }

    public static bool TryGetOwnerControllerId(Hero issueGiver, out string controllerId)
    {
        controllerId = null;
        return issueGiver != null && OwnerControllerIdByIssueGiver.TryGetValue(issueGiver, out controllerId);
    }

    /// <summary>
    /// True only on the one machine whose own <see cref="IControllerIdProvider.ControllerId"/> matches the
    /// recorded owner for this issue - a dedicated server with no local player, or any other non-owning
    /// peer, always reads false.
    /// </summary>
    public static bool IsLocalPeerOwner(Hero issueGiver)
    {
        if (!TryGetOwnerControllerId(issueGiver, out var ownerControllerId)) return false;
        if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return false;

        return controllerIdProvider.ControllerId == ownerControllerId;
    }

    /// <summary>Used by <see cref="Patches.VillageNeedsToolsIssueOwnershipPersistencePatches"/> to save the
    /// registry alongside the behavior's own save record.</summary>
    public static IReadOnlyCollection<KeyValuePair<Hero, string>> Snapshot()
    {
        return OwnerControllerIdByIssueGiver.ToArray();
    }
}
