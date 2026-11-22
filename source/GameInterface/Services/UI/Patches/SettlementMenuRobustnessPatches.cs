using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// A client can receive a party without all of its data (seen right after "Failed to get
/// MobileParty using network handle"). Entering a village or town with that party inside makes
/// <see cref="GameMenuPartyItemVM.RefreshProperties"/> throw a NullReferenceException. The overlay
/// rebuilds its party list every frame, so it throws every frame and the menu freezes (escape and
/// enter do nothing). Swallow only that exception so the broken entry stays blank and the menu works.
/// </summary>
[HarmonyPatch]
internal class SettlementMenuRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementMenuRobustnessPatches>();

    private static int caughtCount;

    internal static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(GameMenuPartyItemVM), nameof(GameMenuPartyItemVM.RefreshProperties)),
        AccessTools.Method(typeof(SettlementMenuOverlayVM), nameof(SettlementMenuOverlayVM.UpdatePartyList)),
    };

    [HarmonyFinalizer]
    internal static Exception Finalizer(Exception __exception, MethodBase __originalMethod)
    {
        if (__exception is not NullReferenceException)
            return __exception;

        // runs every frame while the menu is open, so only log the first one and then every 1000th
        var count = Interlocked.Increment(ref caughtCount);
        if (count == 1 || count % 1000 == 0)
        {
            Logger.Error(__exception, "Skipped a settlement party with missing data in {Method} ({Count} times so far)",
                $"{__originalMethod.DeclaringType}.{__originalMethod.Name}", count);
        }

        return null;
    }
}