using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.Patches
{
    /// <summary>
    /// Stops the map's navigation hotkeys (e.g. "i" -> inventory, "c" -> character) from firing
    /// while a modal inquiry / text-input box is open.
    ///
    /// In co-op the map state is kept active and force-ticked in the background, so
    /// <c>MapScreen.TickNavigationInput</c> keeps running while another screen and an inquiry such
    /// as "Change Clan Name" are on top. It reads the navigation game-keys from the map's scene
    /// layer, whose input-allowed flag is stale (ScreenManager only refreshes top-screen / global
    /// layers), so pressing "i" opens the inventory instead of typing the letter into the text box.
    ///
    /// Skip TickNavigationInput while any inquiry is active so the keystrokes reach the focused text
    /// box instead. This is focus-independent, works for every text-input inquiry, and matches
    /// vanilla's intent that you cannot navigate away while a modal dialog is open.
    /// <see cref="InformationManager.IsAnyInquiryActive"/> is used rather than a focus check because a
    /// layer that loses focus has its focused widget cleared, so a focus check cannot detect the
    /// inquiry. (The persistent map-bar navigation, <c>GauntletMapBarGlobalLayer.HandlePanelSwitching</c>,
    /// is intentionally not patched: it reads the focused top-screen layer, where vanilla already
    /// suppresses navigation while the inquiry layer holds focus.)
    /// </summary>
    [HarmonyPatch(typeof(MapScreen), "TickNavigationInput")]
    internal class NavigationHotkeyDuringInquiryPatches
    {
        // Returning false skips TickNavigationInput (no navigation while a text box is open). Its
        // bool result is ignored by the caller (AfterWaitTick), so the skipped default is fine.
        private static bool Prefix() => !InformationManager.IsAnyInquiryActive();
    }
}
