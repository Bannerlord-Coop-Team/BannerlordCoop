using Common.Util;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Inventory;
using GameInterface.Policies;

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
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        AllowedThread.AllowThisThread();
    }

    static void Postfix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        AllowedThread.RevokeThisThread();
    }
}
