using Common.Messaging;
using GameInterface.Services.Time.Enum;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Time.Messages;

public record AttemptedTimeSpeedChanged : IEvent
{
    public TimeControlEnum NewControlMode { get; }
    public AttemptedTimeSpeedChanged(TimeControlEnum newControlMode)
    {
        NewControlMode = newControlMode;
    }
}
