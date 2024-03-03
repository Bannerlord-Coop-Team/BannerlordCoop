using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Extentions;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Patch for Settlement.LastThreatTime
/// </summary>
[HarmonyPatch(typeof(Settlement))]
internal class LastThreatTimeSettlementPatch
{
    private static ILogger Logger = LogManager.GetLogger<Settlement>();



    [HarmonyPatch(nameof(Settlement.LastThreatTime), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool LastThreatTimePrefix(ref Settlement __instance, ref CampaignTime value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Settlement), Environment.StackTrace);
            return true;
        }



        // can pass null so always ensure to just set the value
        long? numTicks = (value != null) ? value.NumTicks : null;  

        var message = new SettlementChangedLastThreatTime(__instance.StringId, numTicks);

        MessageBroker.Instance.Publish(__instance, message);


        return true;
    }

    internal static void LastThreatTimeChange(Settlement settlement, long? lastThreatTime)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                if (lastThreatTime.HasValue)
                    settlement.LastThreatTime = new CampaignTime(lastThreatTime.Value);
                else
                    settlement.LastThreatTime = CampaignTime.Never;
            }
        });
    }


}
