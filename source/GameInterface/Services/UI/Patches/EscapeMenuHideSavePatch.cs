using Common;
using HarmonyLib;
using SandBox.View.Map;
using System.Collections.Generic;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.ViewModelCollection.EscapeMenu;

namespace GameInterface.Services.UI.Patches
{
    /// <summary>
    /// Hides the "Save", "Save As", "Load" and "Save And Exit" entries in the client's campaign-map escape menu,
    /// because the server saves the campaign and a client can't load its own save into a co-op session.
    /// </summary>
    [HarmonyPatch(typeof(MapScreen), "GetEscapeMenuItems")]
    internal class EscapeMenuHideSavePatch
    {
        private static readonly TextObject[] HiddenButtonTexts =
        {
            new TextObject("{=bV75iwKa}Save"),
            new TextObject("{=e0KdfaNe}Save As"),
            new TextObject("{=9NuttOBC}Load"),
            new TextObject("{=AbEh2y8o}Save And Exit"),
        };

        [HarmonyPostfix]
        static void HideSaveButtons(List<EscapeMenuItemVM> __result)
        {
            if (!ModInformation.IsClient) return;

            // Matched by text since Invite Friends is optional and inserted before these entries, so their indices vary
            foreach (var buttonText in HiddenButtonTexts)
            {
                string actionText = buttonText.ToString();
                __result.RemoveAll(item => item.ActionText == actionText);
            }
        }
    }
}
