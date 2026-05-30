using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Time.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
            $"Connected clients should interpolate to catch up over the next second.";
    }
}