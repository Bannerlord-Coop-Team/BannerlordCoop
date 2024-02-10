using Common.Logging.Attributes;
using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using System;

namespace GameInterface.Services.Heroes.Messages;

[BatchLogMessage]
public record SetTimeControlMode : ICommand
{
    public TimeControlEnum NewTimeMode { get; }

    public SetTimeControlMode(TimeControlEnum newTimeMode)
    {
        NewTimeMode = newTimeMode;
    }
}
[BatchLogMessage]
public record TimeControlModeSet : IResponse
{
    public TimeControlEnum NewTimeMode { get; }

    public TimeControlModeSet(TimeControlEnum newTimeMode)
    {
        NewTimeMode = newTimeMode;
    }
}
