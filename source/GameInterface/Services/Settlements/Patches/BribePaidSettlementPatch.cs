using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using Serilog.Core;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;


/// <summary>
/// Used to sync the Settlement.BribePaid value.
/// </summary>
[HarmonyPatch(typeof(Settlement))]
internal class BribePaidSettlementPatch
{

    private static ILogger Logger = LogManager.GetLogger<Settlement>();

    [HarmonyPatch(nameof(Settlement.BribePaid),MethodType.Setter)]
    [HarmonyPrefix]
    private static bool BribePaidPrefix(ref Settlement __instance, ref int value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Settlement), Environment.StackTrace);
            return true;
        }

        if (__instance.BribePaid == value) return false;


        var message = new SettlementChangedBribePaid(__instance.StringId, value);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    internal static void RunBribePaidChange(Settlement settlement, int bribePaid)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement.BribePaid = bribePaid;
            }
        });
    }

}
