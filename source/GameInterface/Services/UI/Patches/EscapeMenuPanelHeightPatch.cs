using Common;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;

namespace GameInterface.Services.UI.Patches
{
    /// <summary>
    /// Grows the campaign-map escape-menu background pillar so it fits its contents on clients.
    /// </summary>
    [HarmonyPatch(typeof(GauntletLayer), "LoadMovieAux")]
    internal class EscapeMenuPanelHeightPatch
    {
        private const string EscapeMenuMovieName = "EscapeMenu";
        private const string EscapeMenuPanelId = "EscapeMenu";
        private const string ButtonsContainerId = "ButtonsContainer";

        // Stock value is 115, sized so the 8 vanilla items reach the base of the
        // fixed-height pillar. With CoverChildren the pillar grows to its buttons, so this
        // large reserve becomes dead space below the last button.
        private const float ButtonsContainerBottomMargin = 85f;

        // Set by a custom map-menu button patch and consumed by the next movie load.
        internal static bool CustomButtonInserted;

        [HarmonyPostfix]
        static void GrowPanelToFitItems(IGauntletMovie __result)
        {
            if (!ModInformation.IsClient) return;
            if (__result == null || __result.MovieName != EscapeMenuMovieName) return;

            // Character creation and the education screen use this same movie.
            if (!CustomButtonInserted) return;
            CustomButtonInserted = false;

            Widget panel = __result.RootWidget.FindChild(EscapeMenuPanelId, includeAllChildren: true);
            if (panel == null) return;

            panel.HeightSizePolicy = SizePolicy.CoverChildren;

            Widget buttons = panel.FindChild(ButtonsContainerId, includeAllChildren: true);
            if (buttons == null) return;

            buttons.MarginBottom = ButtonsContainerBottomMargin;
        }
    }
}
