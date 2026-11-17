using Common;
using HarmonyLib;
using NavalDLC.ViewModelCollection.Port.PortScreenHandlers;

namespace Coop.Naval.PortScreen.Patches;

/// <summary>
/// Hides the port screen's moor and moored ships buttons on clients, because mooring isn't synced yet (#4603).
/// </summary>
[HarmonyPatch(typeof(PortScreenHandler))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class PortScreenMooringPatches
{
    // An invalid action hides its button, so no ship can be queued for mooring or retrieval
    [HarmonyPatch(nameof(PortScreenHandler.GetCanStashShip))]
    [HarmonyPostfix]
    public static void GetCanStashShipPostfix(ref PortActionInfo __result)
    {
        if (ModInformation.IsClient) __result = PortActionInfo.CreateInvalid();
    }

    [HarmonyPatch(nameof(PortScreenHandler.GetCanViewStash))]
    [HarmonyPostfix]
    public static void GetCanViewStashPostfix(ref PortActionInfo __result)
    {
        if (ModInformation.IsClient) __result = PortActionInfo.CreateInvalid();
    }
}
