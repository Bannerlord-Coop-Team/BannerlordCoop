using Common;
using HarmonyLib;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

[HarmonyPatch(typeof(ItemRoster))]
internal class HeadmanNeedsGrainProgressPatches
{
    [HarmonyPatch(nameof(ItemRoster.AddToCounts), new[] { typeof(EquipmentElement), typeof(int) })]
    [HarmonyPostfix]
    private static void AddPostfix(ItemRoster __instance, EquipmentElement rosterElement)
    {
        if (rosterElement.Item == DefaultItems.Grain) UpdateProgress(__instance);
    }

    [HarmonyPatch(nameof(ItemRoster.Clear))]
    [HarmonyPostfix]
    private static void ClearPostfix(ItemRoster __instance) => UpdateProgress(__instance);

    private static void UpdateProgress(ItemRoster roster)
    {
        if (ModInformation.IsServer || roster != Campaign.Current?.MainParty?.ItemRoster) return;
        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) ||
            !ContainerProvider.TryResolve<IControllerIdProvider>(out var controller)) return;

        foreach (var quest in Campaign.Current.QuestManager.Quests.OfType<Quest>().ToList())
        {
            if (!quest.IsOngoing || quest._playerAcceptedQuestLog == null) continue;
            var issue = IssueManager.GetIssueOfQuest(quest);
            if (issue == null || !owners.TryGetOwnerControllerId(issue, out var owner) || owner != controller.ControllerId) continue;
            quest._playerAcceptedQuestLog.UpdateCurrentProgress(quest.GetRequiredGrainCountOnPlayer());
            quest.CheckIfPlayerReadyToReturnGrains();
        }
    }
}
