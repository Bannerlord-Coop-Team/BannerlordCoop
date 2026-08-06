using GameInterface.Services.Entity;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic;

internal static class IssueOwnershipRegistry
{
    private static readonly PendingRegistry<string> Registry = new();

    public static void SetOwner(Hero issueGiver, string controllerId)
    {
        if (issueGiver == null || string.IsNullOrEmpty(controllerId)) return;

        Registry.Set(issueGiver, controllerId);
    }

    public static void Clear(Hero issueGiver)
    {
        Registry.Clear(issueGiver);
    }

    public static void ClearAll()
    {
        Registry.ClearAll();
    }

    public static bool TryGetOwnerControllerId(Hero issueGiver, out string controllerId)
    {
        return Registry.TryGet(issueGiver, out controllerId);
    }

    public static bool IsLocalPeerOwner(Hero issueGiver)
    {
        if (!TryGetOwnerControllerId(issueGiver, out var ownerControllerId)) return false;
        if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return false;

        return controllerIdProvider.ControllerId == ownerControllerId;
    }

    public static IReadOnlyCollection<KeyValuePair<Hero, string>> Snapshot()
    {
        return Registry.Snapshot();
    }

    public static void RestoreAll(IEnumerable<KeyValuePair<Hero, string>> entries)
    {
        Registry.RestoreAll(entries);
    }
}
