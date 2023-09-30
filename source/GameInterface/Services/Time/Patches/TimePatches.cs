using Common.Extensions;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Campaign))]
internal class TimePatches
{
    private static readonly Action<Campaign, CampaignTimeControlMode> _setTimeControlMode = 
        typeof(Campaign)
        .GetField("_timeControlMode", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildUntypedSetter<Campaign, CampaignTimeControlMode>();
    private static readonly Func<Campaign, CampaignTimeControlMode> _getTimeControlMode =
        typeof(Campaign)
        .GetField("_timeControlMode", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildUntypedGetter<Campaign, CampaignTimeControlMode>();

    [HarmonyPatch("TimeControlMode")]
    [HarmonyPatch(MethodType.Setter)]
    private static bool Prefix(ref Campaign __instance, ref CampaignTimeControlMode value)
    {
        if (__instance.TimeControlModeLock == false &&
            value != _getTimeControlMode(__instance))
        {
            MessageBroker.Instance.Publish(__instance, new AttemptedTimeSpeedChanged(value));
        }

        return false;
    }

    public static void OverrideTimeControlMode(Campaign campaign, CampaignTimeControlMode value)
    {
        if (campaign == null) return;

        _setTimeControlMode(campaign, value);
    }
}
