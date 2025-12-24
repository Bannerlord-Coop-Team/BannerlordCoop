using Common.Messaging;
using GameInterface.Services.Time.Enum;

namespace GameInterface.Services.Time.Messages;

public record GetTimeControlMode : ICommand
{
}

public record TimeControlModeResponse : IResponse
{
    public TimeControlEnum TimeMode { get; }

    public TimeControlModeResponse(TimeControlEnum timeMode)
    {
        TimeMode = timeMode;
    }
}
