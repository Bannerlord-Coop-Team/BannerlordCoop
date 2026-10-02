using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Patches;

internal sealed class HeadmanHerdPersonalOwnershipSaveData
{
    [SaveableField(1)]
    internal string ObjectId;
    [SaveableField(2)]
    internal string ControllerId;

    private HeadmanHerdPersonalOwnershipSaveData() { }

    internal HeadmanHerdPersonalOwnershipSaveData(string objectId, string controllerId)
    {
        ObjectId = objectId;
        ControllerId = controllerId;
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.SyncData))]
internal static class HeadmanHerdPersonalOwnershipPersistencePatch
{
    [HarmonyPostfix]
    internal static void Postfix(IDataStore dataStore)
    {
        if (!ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership)) return;
        List<HeadmanHerdPersonalOwnershipSaveData> records = dataStore.IsSaving
            ? ownership.Snapshot().Select(entry => new HeadmanHerdPersonalOwnershipSaveData(entry.Key, entry.Value)).ToList()
            : null;
        dataStore.SyncData("_coop_headman_herd_personal_ownership", ref records);
        if (dataStore.IsLoading)
            ownership.Restore((records ?? new List<HeadmanHerdPersonalOwnershipSaveData>())
                .Where(entry => entry != null && !string.IsNullOrEmpty(entry.ObjectId) && !string.IsNullOrEmpty(entry.ControllerId))
                .Select(entry => new KeyValuePair<string, string>(entry.ObjectId, entry.ControllerId)));
    }
}
