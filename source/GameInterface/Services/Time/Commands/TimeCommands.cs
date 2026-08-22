using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Time.Interfaces;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Time.Commands;

internal class TimeCommands
{
    [CommandLineArgumentFunction("get_time_mode", "coop.debug")]
    public static string GetTimeMode(List<string> strings)
    {
        if (!ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControlInterface))
        {
            return "Failed to get time control interface";
        }

        return $"{timeControlInterface.GetTimeControl()}";
    }

    [CommandLineArgumentFunction("set_time_mode", "coop.debug")]
    public static string SetTimeMode(List<string> strings)
    {
        if (!ModInformation.IsServer)
        {
            return "set_time_mode must be run on the server/host.";
        }

#if DEBUG
        bool forceForLiveTest = strings.Count == 2 &&
            strings[1].Equals("force-live-test", StringComparison.OrdinalIgnoreCase);
#else
        const bool forceForLiveTest = false;
#endif

        if ((strings.Count != 1 && !forceForLiveTest) ||
            !Enum.TryParse(strings[0], true, out TimeControlEnum timeMode))
        {
            return "Usage: coop.debug.set_time_mode <Pause|Play_1x|Play_2x>";
        }

        if (!ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControlInterface))
        {
            return "Failed to get time control interface";
        }

#if DEBUG
        if (forceForLiveTest)
        {
            timeControlInterface.ServerSetTimeControlForLiveTest(timeMode);
            return $"Time control force-set for live testing to {timeControlInterface.GetTimeControl()}";
        }
#endif

        timeControlInterface.ServerSetTimeControl(timeMode);
        return $"Time control set to {timeControlInterface.GetTimeControl()}";
    }

#if DEBUG
    [CommandLineArgumentFunction("verify_time_mode", "coop.debug")]
    public static string VerifyTimeMode(List<string> strings)
    {
        if (strings.Count != 1 ||
            !Enum.TryParse(strings[0], true, out TimeControlEnum expectedTimeMode))
            return "Usage: coop.debug.verify_time_mode <Pause|Play_1x|Play_2x>";

        if (!ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControlInterface))
            return "Failed to get time control interface";

        return (timeControlInterface.GetTimeControl() == expectedTimeMode).ToString();
    }
#endif

#if DEBUG
    [CommandLineArgumentFunction("request_time_mode", "coop.debug")]
    public static string RequestTimeMode(List<string> strings)
    {
        if (ModInformation.IsServer)
            return "request_time_mode must be run on a client.";
        if (strings.Count != 1 ||
            !Enum.TryParse(strings[0], true, out TimeControlEnum timeMode))
            return "Usage: coop.debug.request_time_mode <Pause|Play_1x|Play_2x>";

        MessageBroker.Instance.Publish(typeof(TimeCommands), new TimeSpeedChangedAttempted(timeMode));
        return $"Requested time control {timeMode}.";
    }
#endif

    [CommandLineArgumentFunction("advance_time", "coop.debug")]
    public static string AdvanceTime(List<string> strings)
    {
        // Time is authoritative on the server; advancing it elsewhere would just be
        // overwritten by the next server sync.
        if (ModInformation.IsClient)
        {
            return "advance_time must be run on the server/host. The server is authoritative for campaign time.";
        }

        if (Campaign.Current == null)
        {
            return "No campaign is currently loaded.";
        }

        float days = 5f;
        if (strings.Count > 0 && float.TryParse(strings[0], out var parsedDays))
        {
            days = parsedDays;
        }

        if (!ContainerProvider.TryResolve<IMapTimeTrackerInterface>(out var mapTimeTrackerInterface))
        {
            return "Failed to get map time tracker interface";
        }

        long ticks = CampaignTime.Days(days).NumTicks;
        mapTimeTrackerInterface.AdvanceTime(ticks);

        return $"Advanced campaign time forward by {days} day(s) ({ticks} ticks). " +
            $"Connected clients should apply the jump on the next time update.";
    }
}
