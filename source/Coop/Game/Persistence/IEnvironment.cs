using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
{
    public interface IEnvironment
    {
        #region TimeControl
        CampaignTimeControlMode? RequestedTimeControlMode { get; set; }
        CampaignTimeControlMode TimeControlMode { get; set; }
        #endregion
    }
}
