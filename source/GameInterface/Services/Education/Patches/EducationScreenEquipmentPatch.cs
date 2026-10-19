using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using SandBox.GauntletUI;

namespace GameInterface.Services.Education.Patches;

[HarmonyPatch(typeof(GauntletEducationScreen), nameof(GauntletEducationScreen.RefreshSceneCharacters))]
internal class EducationScreenEquipmentPatch
{
    [HarmonyPrefix]
    private static void Prefix(out TransientEquipmentSyncScope __state)
    {
        __state = ModInformation.IsClient ? new TransientEquipmentSyncScope() : null;
    }

    [HarmonyFinalizer]
    private static void Finalizer(TransientEquipmentSyncScope __state)
    {
        __state?.Dispose();
    }
}
