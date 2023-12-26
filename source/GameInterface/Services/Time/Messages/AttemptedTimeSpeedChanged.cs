using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

public record AttemptedTimeSpeedChanged : IEvent
{
    public TimeControlEnum NewControlMode { get; }
    public AttemptedTimeSpeedChanged(CampaignTimeControlMode newControlMode)
    {
        switch (newControlMode)
        {
            case CampaignTimeControlMode.Stop:
            case CampaignTimeControlMode.FastForwardStop:
                NewControlMode = TimeControlEnum.Pause;
                break;
            case CampaignTimeControlMode.StoppablePlay:
            case CampaignTimeControlMode.UnstoppablePlay:
                NewControlMode = TimeControlEnum.Play_1x;
                break;
            case CampaignTimeControlMode.UnstoppableFastForward:
            case CampaignTimeControlMode.StoppableFastForward:
                NewControlMode = TimeControlEnum.Play_2x;
                break;
            default:
                throw new InvalidCastException($"{newControlMode} could not be converted to {nameof(TimeControlEnum)}");
        }
    }
}
