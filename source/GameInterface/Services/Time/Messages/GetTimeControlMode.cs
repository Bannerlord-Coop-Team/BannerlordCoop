using Common.Messaging;
using GameInterface.Services.Heroes.Enum;

namespace GameInterface.Services.Heroes.Messages;

public record GetTimeControlMode : ICommand
{
}

public record TimeControlModeResponse : IResponse
{
    public TimeControlEnum TimeMode { get; }

    public TimeControlModeResponse(TimeControlEnum timeMode)
    {
        this.TimeMode = timeMode;
    }
}
