using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
}