using GameInterface.Services.Heroes.Enum;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Time
{
    internal interface ITimeControlModeConverter
    {
        TimeControlEnum Convert(CampaignTimeControlMode mode);
        CampaignTimeControlMode Convert(TimeControlEnum mode);
    }

    internal class TimeControlModeConverter : ITimeControlModeConverter
    {
        public TimeControlEnum Convert(CampaignTimeControlMode mode)
        {
            return mode switch
            {
                CampaignTimeControlMode.Stop => TimeControlEnum.Pause,
                CampaignTimeControlMode.FastForwardStop => TimeControlEnum.Pause,

                CampaignTimeControlMode.StoppablePlay => TimeControlEnum.Play_1x,
                CampaignTimeControlMode.UnstoppablePlay => TimeControlEnum.Play_1x,

                CampaignTimeControlMode.StoppableFastForward => TimeControlEnum.Play_2x,
                CampaignTimeControlMode.UnstoppableFastForward => TimeControlEnum.Play_2x,

                _ => throw new InvalidCastException($"{mode} could not be converted to {nameof(TimeControlEnum)}")
            };
        }

        public CampaignTimeControlMode Convert(TimeControlEnum mode)
        {
            return (CampaignTimeControlMode)mode;
        }
    }
}
