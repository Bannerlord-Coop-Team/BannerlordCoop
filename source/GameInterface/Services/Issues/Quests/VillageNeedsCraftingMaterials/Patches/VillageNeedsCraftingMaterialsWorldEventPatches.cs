using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest;

namespace GameInterface.Services.Issues.Quests.VillageNeedsCraftingMaterials.Patches;

/// <summary>
/// The raid and coercion endings are decided by the server, which runs the quest's listeners as each owning
/// player. The framework relays war, kingdom changes and coercion; raids remain specific to a village giver.
/// </summary>
[HarmonyPatch]
internal class VillageNeedsCraftingMaterialsWorldEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.RaidCompleted))]
    [HarmonyPostfix]
    private static void RaidCompletedPostfix(BattleSideEnum winnerSide, RaidEventComponent raidEvent)
    {
        RunAsOwners(quest => quest.OnRaidCompleted(winnerSide, raidEvent));
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnRaidCompleted))]
    [HarmonyPrefix]
    private static bool QuestOnRaidCompletedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool QuestOnMapEventStartedPrefix() => ModInformation.IsServer;

    private static void RunAsOwners(Action<Quest> listener)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners(listener);
    }
}
