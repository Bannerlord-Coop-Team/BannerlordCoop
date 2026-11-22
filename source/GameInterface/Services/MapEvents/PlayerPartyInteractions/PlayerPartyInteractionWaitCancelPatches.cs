using HarmonyLib;
using SandBox.GauntletUI.Map;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Lets a player waiting on another player's answer cancel the request with the escape key.
[HarmonyPatch(typeof(GauntletMapConversationView))]
internal static class PlayerPartyInteractionWaitCancelPatches
{
    [HarmonyPatch(nameof(GauntletMapConversationView.Tick))]
    [HarmonyPrefix]
    public static bool TickPrefix(GauntletMapConversationView __instance)
    {
        // The same key closes an open escape menu, which also opens on its own when the game loses focus.
        if (!PlayerPartyInteractionDialogState.CanCancelWaiting || __instance.MapScreen?.IsEscapeMenuOpened == true ||
            __instance._layerAsGauntletLayer?.Input.IsHotKeyReleased("ToggleEscapeMenu") != true) return true;

        PlayerPartyInteractionDialogState.CancelWaiting();

        // Skip this frame, otherwise vanilla opens the escape menu on the same key release.
        return false;
    }
}
