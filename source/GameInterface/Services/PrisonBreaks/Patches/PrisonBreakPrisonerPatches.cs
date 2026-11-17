using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.PrisonBreaks.Interfaces;
using GameInterface.Services.PrisonBreaks.Messages;
using HarmonyLib;
using SandBox.Missions.MissionLogics.Towns;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.PrisonBreaks.Patches;

/// <summary>
/// Keeps player heroes out of the prison break prisoner list, and returns an aborted mission to the dungeon menu.
/// </summary>
[HarmonyPatch]
internal class PrisonBreakPrisonerPatches
{
    // The freed prisoner would be an AI agent on the rescuer's client, but only its player controls a player hero.
    [HarmonyPatch(typeof(MBInformationManager), nameof(MBInformationManager.ShowMultiSelectionInquiry))]
    [HarmonyPrefix]
    private static void ShowMultiSelectionInquiryPrefix(MultiSelectionInquiryData data)
    {
        if (!PrisonBreakCampaignBehaviorPatches.IsListingPrisoners) return;

        var hint = GameTexts.FindText("str_coop_prison_break_player_prisoner").ToString();
        var elements = data.InquiryElements;
        for (int i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            if ((element.Identifier as CharacterObject)?.HeroObject?.IsPlayerHero() != true) continue;

            elements[i] = new InquiryElement(element.Identifier, element.Title, element.ImageIdentifier, false, hint);
        }
    }

    // Runs after vanilla switched to a result menu, so an aborted mission's dungeon menu wins.
    [HarmonyPatch(typeof(PrisonBreakMissionController), nameof(PrisonBreakMissionController.OnEndMission))]
    [HarmonyPostfix]
    private static void OnEndMissionPostfix(PrisonBreakMissionController __instance)
    {
        if (!ContainerProvider.TryResolve<IPrisonBreakCampaignBehaviorInterface>(out var behaviorInterface)) return;

        behaviorInterface.OnPrisonBreakMissionEnded();
        MessageBroker.Instance.Publish(__instance, new PrisonBreakMissionEnded(Settlement.CurrentSettlement));
    }
}
