using Common.Util;
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
        AccessTools.Method(typeof(InventoryLogic), nameof(InventoryLogic.ResetLogic)),
    };

    static void Prefix()
    {
        AllowedThread.AllowThisThread();
    }

    static void Postfix()
    {
        AllowedThread.RevokeThisThread();
    }
}
