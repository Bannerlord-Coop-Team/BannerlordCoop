using HarmonyLib;
using System.Collections.Generic;
using GameInterface.Services.Issues.Handlers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.SyncData))]
internal class SmugglersQuestOwnershipPersistencePatch
{
    [HarmonyPostfix]
    private static void Postfix(IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<QuestTraitProgressHandler>(out var traits)) traits.SyncData(dataStore);
        if (!ContainerProvider.TryResolve<ISmugglersQuestOwners>(out var owners)) return;
        Dictionary<MBObjectBase, Hero> saved = dataStore.IsSaving ? owners.Snapshot() : null;
        dataStore.SyncData("_coop_smugglers_quest_owners", ref saved);
        if (dataStore.IsLoading) owners.Restore(saved);
    }
}
