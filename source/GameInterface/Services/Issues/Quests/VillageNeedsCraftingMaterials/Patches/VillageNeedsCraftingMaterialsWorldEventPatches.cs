using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest;

namespace GameInterface.Services.Issues.Quests.VillageNeedsCraftingMaterials.Patches;

/// <summary>
/// The raid and coercion endings are decided by the server, which runs the quest's listeners as each owning
/// player. The framework relays war and kingdom changes; these two events are specific to a village giver.
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

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void OnMapEventStartedPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        // Only forcing supplies or volunteers can be a coercion of the quest giver
        if (!mapEvent.IsForcingSupplies && !mapEvent.IsForcingVolunteers)
        {
            return;
        }

        RunAsOwners(quest => quest.OnMapEventStarted(mapEvent, attackerParty, defenderParty));
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
