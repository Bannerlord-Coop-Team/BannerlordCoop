using Common.Messaging;
using GameInterface.Services.Heroes.Enum;

namespace GameInterface.Services.Heroes.Messages;

public record TimeSpeedChangedAttempted : IEvent
{
    public TimeControlEnum NewControlMode { get; }
    public TimeSpeedChangedAttempted(TimeControlEnum newControlMode)
    {
        NewControlMode = newControlMode;
    }
}
