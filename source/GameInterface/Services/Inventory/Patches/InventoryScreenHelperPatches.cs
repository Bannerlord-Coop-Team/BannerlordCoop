using Common.Util;
using GameInterface.Services.Villages;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Inventory;

namespace GameInterface.Services.Inventory.Patches;

[HarmonyPatch]
internal class InventoryScreenHelperPatches
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(InventoryScreenHelper), nameof(InventoryScreenHelper.OpenScreenAsTrade)),
        AccessTools.Method(typeof(InventoryScreenHelper), nameof(InventoryScreenHelper.OpenInventoryPresentation)),
        AccessTools.Method(typeof(InventoryLogic), nameof(InventoryLogic.TransferItem)),
        AccessTools.Method(typeof(InventoryLogic), nameof(InventoryLogic.ResetLogic)),
        AccessTools.Method(typeof(InventoryLogic), nameof(InventoryLogic.SlaughterItem))
    };

    static void Prefix()
    {
        AllowedThread.AllowThisThread();
    }

    // Finalizer (not postfix) so the revoke runs even when the original throws;
    // a skipped revoke would leave the thread permanently allowed.
    static void Finalizer()
    {
        AllowedThread.RevokeThisThread();
    }
}

[HarmonyPatch(typeof(InventoryScreenHelper), nameof(InventoryScreenHelper.CloseScreen))]
internal class InventoryForceTransferClosePatches
{
    [HarmonyPrefix]
    public static void CloseScreenPrefix(bool fromCancel)
    {
        // Cancel runs Reset(true) then DoneLogic, and the Done prefix would
        // otherwise claim the force attribution as a zero-take commit. Drop
        // the attribution first so the cancel preserves the pool and stops
        // gating unrelated party screens. Done closes keep the slot so the
        // Done prefix can claim it.
        if (fromCancel)
            ForceTransferScreenTracker.Clear();
    }
}
