using Common;
using Common.Messaging;
using GameInterface.Services.CampaignService.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(Campaign))]
public class CampaignTimePatch
{

    [HarmonyPatch("DailyTick")]
    [HarmonyPrefix]
    private static bool NumTicksPrefix(ref Campaign __instance)
    {

        if (ModInformation.IsClient) return true;


        MapTimeTracker tracker = Campaign.Current.MapTimeTracker;

        long numTicks = tracker._numTicks;
        long deltaTime = tracker._deltaTimeInTicks;

        MessageBroker.Instance.Publish(__instance, new CampaignTimeChanged(numTicks, deltaTime));

        return true;
    }

    internal static void RunTimeChange(long numTicks, long deltaTime)
    {

        GameLoopRunner.RunOnMainThread(() =>
        {
            Campaign.Current.MapTimeTracker._numTicks = numTicks;
            Campaign.Current.MapTimeTracker._deltaTimeInTicks = deltaTime;
        });
    }

}
