using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Equipments.Patches;

[HarmonyPatch(typeof(BannerBearerLogic), "CreateBannerEquipmentForAgent")]
internal class BannerBearerEquipmentPatch
{
    [HarmonyPrefix]
    private static void Prefix(out TransientEquipmentSyncScope __state)
    {
        // Banner loadouts are mission copies, not new campaign equipment.
        __state = ModInformation.IsClient ? new TransientEquipmentSyncScope() : null;
    }

    [HarmonyFinalizer]
    private static void Finalizer(TransientEquipmentSyncScope __state)
    {
        __state?.Dispose();
    }
}
