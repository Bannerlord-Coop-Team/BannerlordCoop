using Common;
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

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Adds co-op client options as the third item in the campaign-map escape menu.
/// </summary>
[HarmonyPatch(typeof(MapScreen), "GetEscapeMenuItems")]
internal class EscapeMenuCoopOptionsPatch
{
    [HarmonyPostfix]
    static void AddCoopOptionsItem(List<EscapeMenuItemVM> __result)
    {
        if (ModInformation.IsServer) return;

        __result.Insert(2, new EscapeMenuItemVM(
            new TextObject("Coop Options"),
            _ => ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<CoopOptionsUI>()),
            identifier: null,
            getIsDisabledAndReason: () => new Tuple<bool, TextObject>(false, new TextObject("")),
            isPositiveBehaviored: false));

        EscapeMenuResyncButtonPatch.CustomButtonInserted = true;
    }
}

/// <summary>
/// Adds co-op client options as the third item in the battle escape menu.
/// </summary>
[HarmonyPatch(typeof(MissionGauntletSingleplayerEscapeMenu), "GetEscapeMenuItems")]
internal class MissionEscapeMenuCoopOptionsPatch
{
    [HarmonyPostfix]
    static void AddCoopOptionsItem(MissionGauntletSingleplayerEscapeMenu __instance, List<EscapeMenuItemVM> __result)
    {
        if (ModInformation.IsServer) return;

        __result.Insert(2, new EscapeMenuItemVM(
            new TextObject("Coop Options"),
            _ => OpenCoopOptions(__instance),
            identifier: null,
            getIsDisabledAndReason: () => new Tuple<bool, TextObject>(false, new TextObject("")),
            isPositiveBehaviored: false));
    }

    private static void OpenCoopOptions(MissionGauntletSingleplayerEscapeMenu escapeMenu)
    {
        var owner = ScreenManager.TopScreen;
        escapeMenu.OnEscape();
        CoopOptionsOverlay.Show(owner);
    }
}
