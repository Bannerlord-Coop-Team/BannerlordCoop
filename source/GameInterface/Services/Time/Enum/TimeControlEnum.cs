using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Time.Enum;

public enum TimeControlEnum
{
    Pause = CampaignTimeControlMode.Stop,
    Play_1x = CampaignTimeControlMode.StoppablePlay,
    Play_2x = CampaignTimeControlMode.StoppableFastForward,
}
