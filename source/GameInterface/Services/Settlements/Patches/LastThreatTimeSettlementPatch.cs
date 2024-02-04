using Common;
using Common.Messaging;
using Common.Util;
using Coop.Mod.Extentions;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;


[HarmonyPatch(typeof(Settlement))]
internal class LastThreatTimeSettlementPatch
{
    private static readonly PropertyInfo LastThreatTime = typeof(Settlement).GetProperty(nameof(Settlement.LastThreatTime));

    [HarmonyPatch(nameof(Settlement.LastThreatTime), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool LastThreatTimePrefix(ref Settlement __instance, ref CampaignTime value)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (PolicyProvider.AllowOriginalCalls) return true;

        if (ModInformation.IsClient) return false;


        // can pass null so always ensure to just set the value
        long? numTicks = (value != null) ? value.GetNumTicks() : null;  

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
                // only thing that essentially happens is it calls CampaignTime.Now which creates new CampaignTime(numTicks)
                // only updates ticks so just set tick value.
                if(lastThreatTime.HasValue)
                    settlement.LastThreatTime.SetNumTicks(lastThreatTime.Value);    
                else
                    LastThreatTime.SetValue(settlement, lastThreatTime);
            }
        });

    }


}
