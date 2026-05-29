using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

public record TimeSpeedChangedAttempted : IEvent
{
    public TimeControlEnum NewControlMode { get; }
    public TimeSpeedChangedAttempted(TimeControlEnum newControlMode)
    {
        NewControlMode = newControlMode;
    }
}
