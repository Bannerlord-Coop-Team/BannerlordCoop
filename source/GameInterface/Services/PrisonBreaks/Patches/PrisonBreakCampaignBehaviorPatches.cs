using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using GameInterface.Services.PrisonBreaks.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PrisonBreaks.Patches;

[HarmonyPatch(typeof(PrisonBreakCampaignBehavior))]
internal class PrisonBreakCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.OnSessionLaunched))]
    [HarmonyPrefix]
    private static bool OnSessionLaunchedPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.CanHeroDie))]
    [HarmonyPrefix]
    private static bool CanHeroDiePrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.LocationCharactersAreReadyToSpawn))]
    [HarmonyPrefix]
    private static bool LocationCharactersAreReadyToSpawnPrefix(PrisonBreakCampaignBehavior __instance, ref IDisposable __state)
    {
        if (ModInformation.IsServer) return false;

        // The location patches only let a client add its own party heroes, but this prison roster only
        // exists for the local mission, so the prisoner is added without being sent to the server.
        if (__instance._launchingPrisonBreakMission)
        {
            __state = CallOriginalPolicy.AllowOriginalsForCurrentOperation();
        }

        return true;
    }

    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.LocationCharactersAreReadyToSpawn))]
    [HarmonyFinalizer]
    private static void LocationCharactersAreReadyToSpawnFinalizer(IDisposable __state) => __state?.Dispose();

    // Guard loadouts are mission copies, not new campaign equipment.
    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.GetPrisonGuardAgentData))]
    [HarmonyPrefix]
    private static void GetPrisonGuardAgentDataPrefix(out IDisposable __state)
    {
        __state = ModInformation.IsClient ? new TransientEquipmentSyncScope() : null;
    }

    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.GetPrisonGuardAgentData))]
    [HarmonyFinalizer]
    private static void GetPrisonGuardAgentDataFinalizer(IDisposable __state) => __state?.Dispose();

    internal static bool IsListingPrisoners;

    // Marks the prisoner inquiry this consequence shows, see PrisonBreakPrisonerPatches.
    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.game_menu_castle_prison_break_on_consequence))]
    [HarmonyPrefix]
    private static void ListPrisonersPrefix() => IsListingPrisoners = true;

    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.game_menu_castle_prison_break_on_consequence))]
    [HarmonyFinalizer]
    private static void ListPrisonersFinalizer() => IsListingPrisoners = false;

    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.OpenPrisonBreakMission))]
    [HarmonyPrefix]
    private static bool OpenPrisonBreakMissionPrefix(PrisonBreakCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return true;

        MessageBroker.Instance.Publish(__instance, new PrisonBreakStartRequested(Hero.MainHero, Settlement.CurrentSettlement, __instance._prisonerHero));

        return false;
    }

    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.prison_break_end_with_success_on_consequence))]
    [HarmonyPrefix]
    private static bool SuccessPrefix(PrisonBreakCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return true;

        PublishEnded(__instance, PrisonBreakOutcome.Succeeded);

        return false;
    }

    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.prison_break_end_with_success_companion))]
    [HarmonyPrefix]
    private static bool SuccessCompanionPrefix(PrisonBreakCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return true;

        PublishEnded(__instance, PrisonBreakOutcome.SucceededJoinedParty);

        return false;
    }

    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.settlement_prison_break_fail_player_unconscious_continue_on_consequence))]
    [HarmonyPrefix]
    private static bool PlayerUnconsciousPrefix(PrisonBreakCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return true;

        // Same as the sneak-in capture, the party stays in the settlement and the server imprisons the hero.
        GameMenu.ExitToLast();
        PublishEnded(__instance, PrisonBreakOutcome.PlayerCaptured);
        return false;
    }

    [HarmonyPatch(nameof(PrisonBreakCampaignBehavior.settlement_prison_break_fail_prisoner_unconscious_continue_on_consequence))]
    [HarmonyPrefix]
    private static void PrisonerUnconsciousPrefix(PrisonBreakCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return;

        MessageBroker.Instance.Publish(__instance, new PrisonBreakEnded(PrisonBreakOutcome.PrisonerLeftBehind));
    }

    private static void PublishEnded(PrisonBreakCampaignBehavior instance, PrisonBreakOutcome outcome)
    {
        MessageBroker.Instance.Publish(instance, new PrisonBreakEnded(outcome));
        instance._prisonerHero = null;
    }
}
