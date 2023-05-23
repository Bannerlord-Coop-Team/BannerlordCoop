using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using System;

namespace GameInterface.Services.Heroes.Messages;

public record SetTimeControlMode : ICommand
{
    public TimeControlEnum NewTimeMode { get; }

    public SetTimeControlMode(TimeControlEnum newTimeMode)
    {
        NewTimeMode = newTimeMode;
    }
}

public record TimeControlModeSet : IResponse
{
    public TimeControlEnum NewTimeMode { get; }

    public TimeControlModeSet(TimeControlEnum newTimeMode)
    {
        NewTimeMode = newTimeMode;
    }
}
