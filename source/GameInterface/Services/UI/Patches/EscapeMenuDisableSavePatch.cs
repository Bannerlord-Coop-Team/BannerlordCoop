using Common;
using HarmonyLib;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.ViewModelCollection.EscapeMenu;

namespace GameInterface.Services.UI.Patches
{
    /// <summary>
    /// Greys out the "Save" and "Save As" buttons in the client's campaign-map escape menu.
    /// </summary>
    [HarmonyPatch(typeof(MapScreen), "GetEscapeMenuItems")]
    internal class EscapeMenuDisableSavePatch
    {
        // EscapeMenuItemVM.RefreshValues() recomputes IsDisabled from the item's disabled-func
        // on every refresh, so simply setting IsDisabled = true would be overwritten. Instead
        // the item is replaced with a clone whose func always reports disabled.
        private static readonly TextObject SaveButtonText = new TextObject("{=bV75iwKa}Save");
        private static readonly TextObject SaveAsButtonText = new TextObject("{=e0KdfaNe}Save As");
        private static readonly TextObject DisabledReason =
            new TextObject("Saving is disabled on clients; the host saves the campaign.");

        [HarmonyPostfix]
        static void DisableSaveButtons(List<EscapeMenuItemVM> __result)
        {
            if (!ModInformation.IsClient) return;

            DisableItem(__result, SaveButtonText);
            DisableItem(__result, SaveAsButtonText);
        }

        private static void DisableItem(List<EscapeMenuItemVM> items, TextObject buttonText)
        {
            // Invite Friends is optional and inserted before these entries, so their indices vary.
            string actionText = buttonText.ToString();
            int index = items.FindIndex(item => item.ActionText == actionText);
            if (index < 0) return;

            EscapeMenuItemVM original = items[index];
            items[index] = new EscapeMenuItemVM(
                new TextObject(original.ActionText),
                _ => { },
                identifier: null,
                getIsDisabledAndReason: () => new Tuple<bool, TextObject>(true, DisabledReason),
                isPositiveBehaviored: false);
        }
    }
}
