using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Time;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Campaign))]
internal class TimePatches
{
    private static CampaignTimeControlMode CurrentMode = CampaignTimeControlMode.Stop;

    private static readonly Action<Campaign, CampaignTimeControlMode> _setTimeControlMode = 
        typeof(Campaign)
        .GetField("_timeControlMode", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildUntypedSetter<Campaign, CampaignTimeControlMode>();
    private static readonly Func<Campaign, CampaignTimeControlMode> _getTimeControlMode =
        typeof(Campaign)
        .GetField("_timeControlMode", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildUntypedGetter<Campaign, CampaignTimeControlMode>();

    private static readonly TimeControlModeConverter timeControlModeConverter = new();

    [HarmonyPatch("TimeControlMode")]
    [HarmonyPatch(MethodType.Setter)]
    private static bool Prefix(ref Campaign __instance, ref CampaignTimeControlMode value)
    {
        // We only want ExecuteTimeControlChange, which is called from the time control button clicks,
        // to publish the TimeSpeedChanged message.
        // To do this we skip this method if this thread is not "allowed"
        // We set this thread to "allowed" in AllowTimeControlFromControlsPatches
        if (AllowedThread.IsThisThreadAllowed() == false) return false;

        if (value != _getTimeControlMode(__instance))
        {
            var controlMode = timeControlModeConverter.Convert(value);
            MessageBroker.Instance.Publish(__instance, new AttemptedTimeSpeedChanged(controlMode));
        }

        return false;
    }

    [HarmonyPatch("TimeControlMode")]
    [HarmonyPatch(MethodType.Getter)]
    private static void Postfix(ref CampaignTimeControlMode __result)
    {
        __result = CurrentMode;
    }

    public static void OverrideTimeControlMode(Campaign campaign, CampaignTimeControlMode value)
    {
        if (campaign == null) return;

        // _timeControlMode is getting set magically somewhere so we use our own value instead
        CurrentMode = value;
        _setTimeControlMode(campaign, value);
    }
}

[HarmonyPatch(typeof(MapTimeControlVM), "ExecuteTimeControlChange")]
internal class AllowTimeControlFromControlsPatches
{
    private static void Prefix()
    {
        AllowedThread.AllowThisThread();
    }

    private static void Postfix()
    {
        AllowedThread.RevokeThisThread();
    }
}