using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
{
    public interface IEnvironmentServer
    {
        #region TimeControl
        CampaignTimeControlMode TimeControlMode { get; set; }
        #endregion
    }
}
