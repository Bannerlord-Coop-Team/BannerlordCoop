using Common.Commands;
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
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public sealed class GetTimeModeCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug";

        public string Name => "get_time_mode";

        public string Description => "Reports get time mode.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (!ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControlInterface))
            {
                return Failed("Failed to get time control interface");
            }

            return Succeeded($"{timeControlInterface.GetTimeControl()}");
        }
    }

    public sealed class SetTimeModeCoopCommand : ICoopCommand
    {
            public string Prefix => "coop.debug";

            public string Name => "set_time_mode";

            public string Description => "Runs the set time mode debug operation.";

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("time_mode", "Pause, Play_1x, or Play_2x.", isRequired: true),
        #if DEBUG
                new ExpectedArgs("force_live_test", "The DEBUG live-test override token.", isRequired: false),
        #endif
            };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
                    if (!ModInformation.IsServer)
                    {
                        return Failed("set_time_mode must be run on the server/host.");
                    }

            #if DEBUG
                    bool forceForLiveTest = strings.Count == 2 &&
                        strings[1].Equals("force-live-test", StringComparison.OrdinalIgnoreCase);
                    if (strings.Count == 2 && !forceForLiveTest)
                        return Failed("The optional override token must be force-live-test.");
            #endif

                    if (!Enum.TryParse(strings[0], true, out TimeControlEnum timeMode))
                        return Failed($"Invalid time mode '{strings[0]}'. Expected Pause, Play_1x, or Play_2x.");

                    if (!ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControlInterface))
                    {
                        return Failed("Failed to get time control interface");
                    }

            #if DEBUG
                    if (forceForLiveTest)
                    {
                        timeControlInterface.ServerSetTimeControlForLiveTest(timeMode);
                        return Succeeded($"Time control force-set for live testing to {timeControlInterface.GetTimeControl()}");
                    }
            #endif

                    timeControlInterface.ServerSetTimeControl(timeMode);
                    return Succeeded($"Time control set to {timeControlInterface.GetTimeControl()}");
        }
    }

#if DEBUG
    public sealed class RequestTimeModeCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug";

        public string Name => "request_time_mode";

        public string Description => "Runs the request time mode debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("time_mode", "Pause, Play_1x, or Play_2x.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsServer)
                return Failed("request_time_mode must be run on a client.");
            if (!Enum.TryParse(strings[0], true, out TimeControlEnum timeMode))
                return Failed($"Invalid time mode '{strings[0]}'. Expected Pause, Play_1x, or Play_2x.");

            MessageBroker.Instance.Publish(typeof(TimeCommands), new TimeSpeedChangedAttempted(timeMode));
            return Succeeded($"Requested time control {timeMode}.");
        }
    }
#endif

    public sealed class AdvanceTimeCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug";

        public string Name => "advance_time";

        public string Description => "Runs the advance time debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("days", "The number of campaign days to advance.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            // Time is authoritative on the server; advancing it elsewhere would just be
            // overwritten by the next server sync.
            if (ModInformation.IsClient)
            {
                return Failed("advance_time must be run on the server/host. The server is authoritative for campaign time.");
            }

            if (Campaign.Current == null)
            {
                return Failed("No campaign is currently loaded.");
            }

            float days = 5f;
            if (strings.Count > 0)
            {
                if (!float.TryParse(strings[0], out days))
                    return Failed("Days must be a valid number.");
            }

            if (!ContainerProvider.TryResolve<IMapTimeTrackerInterface>(out var mapTimeTrackerInterface))
            {
                return Failed("Failed to get map time tracker interface");
            }

            long ticks = CampaignTime.Days(days).NumTicks;
            mapTimeTrackerInterface.AdvanceTime(ticks);

            return Succeeded($"Advanced campaign time forward by {days} day(s) ({ticks} ticks). " +
                $"Connected clients should apply the jump on the next time update.");
        }
    }
}
