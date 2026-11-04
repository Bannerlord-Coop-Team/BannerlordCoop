using Common;
using GameInterface.Services.MobileParties;
using GameInterface.Services.UI.BugReporting;
using GameInterface.Services.UI.CoopOptions;
using HarmonyLib;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.ViewModelCollection.EscapeMenu;
using TaleWorlds.ScreenSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Adds co-op client options, bug reporting and the unstuck request to the campaign-map escape menu.
/// </summary>
[HarmonyPatch(typeof(MapScreen), "GetEscapeMenuItems")]
internal class EscapeMenuCoopOptionsPatch
{
    private static readonly TextObject CampaignOptionsText = new TextObject("{=PXT6aA4J}Campaign Options");

    private static readonly TextObject BugReportUnavailableDuringConversation =
        new TextObject("Bug reporting is unavailable during conversations.");

    private static readonly TextObject UnstuckText = new TextObject("I'm stuck");

    private static readonly TextObject UnstuckConfirmText = new TextObject(
        "Move your party out of whatever it is stuck in? Your party leaves any battle, siege, settlement or menu " +
        "it is in, and any army it follows. An army you lead stays together.");

    [HarmonyPostfix]
    static void AddCoopOptionsItem(MapScreen __instance, List<EscapeMenuItemVM> __result)
    {
        if (ModInformation.IsServer) return;

        int campaignOptionsIndex = __result.FindIndex(item => item.ActionText == CampaignOptionsText.ToString());
        if (campaignOptionsIndex < 0) return;

        int coopOptionsIndex = campaignOptionsIndex + 1;
        __result.Insert(coopOptionsIndex, new EscapeMenuItemVM(
            new TextObject("Coop Options"),
            _ => ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<CoopOptionsUI>()),
            identifier: null,
            getIsDisabledAndReason: () => new Tuple<bool, TextObject>(false, new TextObject("")),
            isPositiveBehaviored: false));

        int nextIndex = coopOptionsIndex + 1;
        if (TryGetAvailableBugReportOverlay(out var overlay))
        {
            __result.Insert(nextIndex++, new EscapeMenuItemVM(
                new TextObject("Report Coop Bug"),
                _ => OpenBugReport(__instance, overlay),
                identifier: null,
                getIsDisabledAndReason: GetBugReportDisabledState,
                isPositiveBehaviored: false));
        }

        if (ContainerProvider.TryResolve(out IPlayerUnstuckRequester unstuckRequester))
        {
            __result.Insert(nextIndex, new EscapeMenuItemVM(
                UnstuckText,
                _ => OpenUnstuckConfirm(__instance, unstuckRequester),
                identifier: null,
                getIsDisabledAndReason: () => new Tuple<bool, TextObject>(false, new TextObject("")),
                isPositiveBehaviored: false));
        }

        EscapeMenuPanelHeightPatch.CustomButtonInserted = true;
    }

    private static bool TryGetAvailableBugReportOverlay(out IBugReportOverlay overlay)
    {
        return ContainerProvider.TryResolve(out overlay) && overlay.IsAvailable;
    }

    private static Tuple<bool, TextObject> GetBugReportDisabledState()
    {
        if (IsConversationInProgress())
        {
            return new Tuple<bool, TextObject>(true, BugReportUnavailableDuringConversation);
        }
        
        return new Tuple<bool, TextObject>(false, new TextObject(""));
    }

    internal static bool CanOpenBugReport(bool isAvailable, bool isConversationInProgress)
    {
        return isAvailable && !isConversationInProgress;
    }

    private static bool IsConversationInProgress()
    {
        return Campaign.Current?.ConversationManager?.IsConversationInProgress == true;
    }

    private static void OpenBugReport(MapScreen mapScreen, IBugReportOverlay overlay)
    {
        if (!CanOpenBugReport(overlay.IsAvailable, IsConversationInProgress())) return;
        mapScreen.OnEscapeMenuToggled(false);
        overlay.Open();
    }

    private static void OpenUnstuckConfirm(MapScreen mapScreen, IPlayerUnstuckRequester requester)
    {
        mapScreen.OnEscapeMenuToggled(false);
        ShowUnstuckConfirm(requester);
    }

    internal static void ShowUnstuckConfirm(IPlayerUnstuckRequester requester)
    {
        InformationManager.ShowInquiry(new InquiryData(
            UnstuckText.ToString(),
            UnstuckConfirmText.ToString(),
            true, true,
            GameTexts.FindText("str_yes").ToString(),
            GameTexts.FindText("str_no").ToString(),
            () => requester.TryRequest(), null));
    }
}

/// <summary>
/// Adds co-op client options and bug reporting to the battle escape menu.
/// </summary>
[HarmonyPatch(typeof(MissionGauntletSingleplayerEscapeMenu), "GetEscapeMenuItems")]
internal class MissionEscapeMenuCoopOptionsPatch
{
    [HarmonyPostfix]
    static void AddCoopOptionsItem(MissionGauntletSingleplayerEscapeMenu __instance, List<EscapeMenuItemVM> __result)
    {
        if (ModInformation.IsServer) return;

        const int coopOptionsIndex = 2;
        __result.Insert(coopOptionsIndex, new EscapeMenuItemVM(
            new TextObject("Coop Options"),
            _ => OpenCoopOptions(__instance),
            identifier: null,
            getIsDisabledAndReason: () => new Tuple<bool, TextObject>(false, new TextObject("")),
            isPositiveBehaviored: false));

        if (TryGetAvailableBugReportOverlay(out var overlay))
        {
            __result.Insert(coopOptionsIndex + 1, new EscapeMenuItemVM(
                new TextObject("Report Coop Bug"),
                _ => OpenBugReport(__instance, overlay),
                identifier: null,
                getIsDisabledAndReason: () => new Tuple<bool, TextObject>(false, new TextObject("")),
                isPositiveBehaviored: false));
        }
    }

    private static void OpenCoopOptions(MissionGauntletSingleplayerEscapeMenu escapeMenu)
    {
        var owner = ScreenManager.TopScreen;
        escapeMenu.OnEscape();
        CoopOptionsOverlay.Show(owner);
    }

    private static bool TryGetAvailableBugReportOverlay(out IBugReportOverlay overlay)
    {
        return ContainerProvider.TryResolve(out overlay) && overlay.IsAvailable;
    }

    private static void OpenBugReport(
        MissionGauntletSingleplayerEscapeMenu escapeMenu,
        IBugReportOverlay overlay)
    {
        escapeMenu.OnEscape();
        overlay.Open();
    }
}
