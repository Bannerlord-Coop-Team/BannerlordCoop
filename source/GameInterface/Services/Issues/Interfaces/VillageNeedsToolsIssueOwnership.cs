using GameInterface.Services.Entity;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Interfaces;

// Fragile: persistence piggybacks on VillageNeedsToolsIssueBehavior.SyncData. If Tools is ever removed while
// Crafting Materials remains, this registry's persistence silently breaks.
internal static class VillageNeedsToolsIssueOwnership
{
    private static readonly Dictionary<Hero, string> OwnerControllerIdByIssueGiver = new();

    public static void SetOwner(Hero issueGiver, string controllerId)
    {
        if (issueGiver == null || string.IsNullOrEmpty(controllerId)) return;

        OwnerControllerIdByIssueGiver[issueGiver] = controllerId;
    }

    public static void Clear(Hero issueGiver)
    {
        if (issueGiver == null) return;

        OwnerControllerIdByIssueGiver.Remove(issueGiver);
    }

    public static void ClearAll()
    {
        OwnerControllerIdByIssueGiver.Clear();
    }

    public static bool TryGetOwnerControllerId(Hero issueGiver, out string controllerId)
    {
        controllerId = null;
        return issueGiver != null && OwnerControllerIdByIssueGiver.TryGetValue(issueGiver, out controllerId);
    }

    // Only ever set from the server's confirmed broadcast, never optimistically - so there's a brief window on
    // the genuine accepter's own machine, before their own confirmation arrives, where this reads false for
    // them too. Deliberate: turning the quest in is always a separate, later conversation, so the window never
    // matters in practice.
    public static bool IsLocalPeerOwner(Hero issueGiver)
    {
        if (!TryGetOwnerControllerId(issueGiver, out var ownerControllerId)) return false;
        if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return false;

        return controllerIdProvider.ControllerId == ownerControllerId;
    }

    public static IReadOnlyCollection<KeyValuePair<Hero, string>> Snapshot()
    {
        return OwnerControllerIdByIssueGiver.ToArray();
    }
}
