using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Framework.Registries;

/// <summary>
/// Saves which player solves each issue with the campaign. Without it a server restart forgets every owner, and
/// <c>ActiveIssueDismisser</c> then ends each alternative solution and drops the troops a player sent (#3982).
/// After a restart the server sees the same owners as when those players disconnect during a session.
/// </summary>
internal static class IssueOwnershipPersistence
{
    internal const string IssueIdsKey = "coop_issue_owner_issue_ids";
    internal const string ControllerIdsKey = "coop_issue_owner_controller_ids";

    /// <summary>Writes the owners on a save; on a load, restores them into <paramref name="ownership"/> when <paramref name="restore"/> is true.</summary>
    internal static void Sync(IDataStore dataStore, IIssueOwnershipRegistry ownership, bool restore)
    {
        Dictionary<string, string> issueIds = null;
        Dictionary<string, string> controllerIds = null;
        if (dataStore.IsSaving)
        {
            issueIds = new Dictionary<string, string>();
            controllerIds = new Dictionary<string, string>();
            foreach (var owner in ownership.GetAll())
            {
                issueIds[owner.IssueOwnerId] = owner.IssueId;
                controllerIds[owner.IssueOwnerId] = owner.ControllerId;
            }
        }

        dataStore.SyncData(IssueIdsKey, ref issueIds);
        dataStore.SyncData(ControllerIdsKey, ref controllerIds);
        if (!dataStore.IsLoading || !restore || issueIds == null || controllerIds == null) return;

        foreach (var entry in issueIds)
        {
            if (string.IsNullOrEmpty(entry.Value) || !controllerIds.TryGetValue(entry.Key, out var controllerId) ||
                string.IsNullOrEmpty(controllerId)) continue;

            ownership.TrySetOwner(entry.Key, entry.Value, controllerId);
        }
    }
}

/// <summary>Vanilla's <see cref="IssuesCampaignBehavior"/> saves nothing of its own, so its data store carries the owners.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.SyncData))]
internal class IssueOwnershipSavePatch
{
    // Only the server restores: a client learns the owners from the server's join snapshot.
    [HarmonyPostfix]
    private static void Postfix(IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership))
            IssueOwnershipPersistence.Sync(dataStore, ownership, restore: ModInformation.IsServer);
    }
}
