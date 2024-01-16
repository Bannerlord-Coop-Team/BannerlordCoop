using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

public record AttemptedTimeSpeedChanged : IEvent
{
    public TimeControlEnum NewControlMode { get; }
    public AttemptedTimeSpeedChanged(TimeControlEnum newControlMode)
    {
        NewControlMode = newControlMode;
    }
}
