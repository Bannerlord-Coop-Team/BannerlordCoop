using Common.Util;
using HarmonyLib;
using SandBox.View.Map.Visuals;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.PartyVisuals.Patches;

[HarmonyPatch(typeof(MobilePartyVisual))]
internal class AllowMobilePartyVisualPatches
{
    [HarmonyPatch(nameof(MobilePartyVisual.RefreshPartyIcon))]
    [HarmonyPrefix]
    private static void PrefixRefreshValue()
    {
        AllowedThread.AllowThisThread();
    }

    // Finalizers (not postfixes) so the revoke runs even when the original throws;
    // a skipped revoke would leave the thread permanently allowed.
    [HarmonyPatch(nameof(MobilePartyVisual.RefreshPartyIcon))]
    [HarmonyFinalizer]
    private static void FinalizerRefreshValue()
    {
        AllowedThread.RevokeThisThread();
    }
}
