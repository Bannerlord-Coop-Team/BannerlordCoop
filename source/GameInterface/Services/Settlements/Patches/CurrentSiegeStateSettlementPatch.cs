using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Settlements.Settlement;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using Common.Messaging;
using Common;
using System.Reflection;
using GameInterface.Extentions;
using System;
using Common.Logging;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Used to handle CurrentSiegeStatus
/// </summary>

[HarmonyPatch(typeof(Settlement))]
public class CurrentSiegeStateSettlementPatch
{
    private static ILogger Logger = LogManager.GetLogger<Settlement>();

    [HarmonyPatch(nameof(Settlement.CurrentSiegeState), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool CurrentSiegeStatePrefix(ref Settlement __instance, ref SiegeState value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Settlement), Environment.StackTrace);
            return true;
        }

        var message = new SettlementChangedCurrentSiegeState(__instance.StringId, (short)value);

        MessageBroker.Instance.Publish(__instance, message);
        return true;
    }

    internal static void RunCurrentSiegeStateChange(Settlement settlement, SiegeState siegeState)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement.CurrentSiegeState = siegeState;
            }
        });
    }
}
