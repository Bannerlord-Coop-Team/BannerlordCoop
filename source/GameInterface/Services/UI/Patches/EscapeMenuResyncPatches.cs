using Common;
using HarmonyLib;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.ViewModelCollection.EscapeMenu;

namespace GameInterface.Services.UI.Patches
{
    /// <summary>
    /// Adds a "Resync with server" button to the client's campaign-map escape menu,
    /// placed between "Load" and "Save And Exit". The button is a placeholder: clicking
    /// it only informs the player that the feature is not yet implemented. It is shown on
    /// clients only (not the host), since resyncing with the server is meaningless for the
    /// host that owns the authoritative state.
    /// </summary>
    [HarmonyPatch(typeof(MapScreen), "GetEscapeMenuItems")]
    internal class EscapeMenuResyncButtonPatch
    {
        // The stock campaign-map escape menu items are, in order:
        // 0 Return to the Game, 1 Campaign Options, 2 Options, 3 Save, 4 Save As,
        // 5 Load, 6 Save And Exit, 7 Exit to Main Menu. Index 6 places the button
        // directly under "Load" (between "Load" and "Save And Exit").
        private const int ResyncButtonIndex = 6;

        /// <summary>
        /// Set when the resync button has just been inserted — which happens only here, on
        /// the campaign-map escape menu. <see cref="EscapeMenuPanelHeightPatch"/> consumes
        /// this on the immediately-following "EscapeMenu" movie load so the panel resize is
        /// applied only to the map menu. The same "EscapeMenu" movie/prefab is reused by
        /// character creation and the education screen, which must keep their stock layout.
        /// Read and written only on the UI thread during a single escape-menu open.
        /// </summary>
        internal static bool ResyncButtonInserted;

        [HarmonyPostfix]
        static void InsertResyncButton(List<EscapeMenuItemVM> __result)
        {
            if (!ModInformation.IsClient) return;

            __result.Insert(ResyncButtonIndex, new EscapeMenuItemVM(
                new TextObject("Resync with server"),
                _ => InformationManager.DisplayMessage(
                    new InformationMessage("Resync with server is not implemented yet.")),
                identifier: null,
                getIsDisabledAndReason: () => new Tuple<bool, TextObject>(false, new TextObject(string.Empty)),
                isPositiveBehaviored: false));

            ResyncButtonInserted = true;
        }
    }

    /// <summary>
    /// Grows the campaign-map escape-menu background pillar so it fits its contents on
    /// clients. The stock "EscapeMenu" prefab fixes the pillar to the height of the 8
    /// vanilla items, so the extra "Resync with server" item added by
    /// <see cref="EscapeMenuResyncButtonPatch"/> overflows the bottom of the panel.
    /// Switching the panel's height policy to <see cref="SizePolicy.CoverChildren"/> makes
    /// it size to its buttons (one row taller than vanilla for the client's 9 items), and
    /// the button list's bottom margin — which the stock prefab over-sizes for the
    /// fixed-height background — is trimmed so the last button sits just above the base cap
    /// instead of leaving dead space. A prefab override cannot be used because Bannerlord
    /// rejects duplicate prefab names ("This prefab has already been added").
    /// </summary>
    [HarmonyPatch(typeof(GauntletLayer), "LoadMovieAux")]
    internal class EscapeMenuPanelHeightPatch
    {
        private const string EscapeMenuMovieName = "EscapeMenu";
        private const string EscapeMenuPanelId = "EscapeMenu";
        private const string ButtonsContainerId = "ButtonsContainer";

        // Stock value is 115, sized so the 8 vanilla items reach the base of the
        // fixed-height pillar. With CoverChildren the pillar grows to its buttons, so this
        // large reserve becomes dead space below the last button; tuned down to sit the
        // last button just above the base cap.
        private const float ButtonsContainerBottomMargin = 85f;

        [HarmonyPostfix]
        static void GrowPanelToFitItems(IGauntletMovie __result)
        {
            if (!ModInformation.IsClient) return;
            if (__result == null || __result.MovieName != EscapeMenuMovieName) return;

            // Only the campaign-map escape menu carries the resync button, so only it should
            // be resized. The same "EscapeMenu" movie is also loaded by character creation and
            // the education screen; consume the one-shot flag the button patch set on the
            // immediately-preceding MapScreen.GetEscapeMenuItems so those keep their stock layout.
            if (!EscapeMenuResyncButtonPatch.ResyncButtonInserted) return;
            EscapeMenuResyncButtonPatch.ResyncButtonInserted = false;

            Widget panel = __result.RootWidget?.FindChild(EscapeMenuPanelId, includeAllChildren: true);
            if (panel == null) return;

            panel.HeightSizePolicy = SizePolicy.CoverChildren;

            Widget buttons = panel.FindChild(ButtonsContainerId, includeAllChildren: true);
            if (buttons != null)
            {
                buttons.MarginBottom = ButtonsContainerBottomMargin;
            }
        }
    }
}
