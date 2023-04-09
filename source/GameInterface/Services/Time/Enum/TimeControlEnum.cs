using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Time.Enum
{
    public enum TimeControlEnum
    {
        Pause = CampaignTimeControlMode.Stop,
        Play_1x = CampaignTimeControlMode.StoppablePlay,
        Play_2x = CampaignTimeControlMode.StoppableFastForward,
    }
}
