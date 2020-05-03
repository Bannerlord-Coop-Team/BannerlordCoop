using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.World
{
    public class World
    {
        #region synced data
        public CampaignTimeControlMode TimeControlMode { get; set; }
        #endregion

        public void Reset()
        {
            TimeControlMode = CampaignTimeControlMode.Stop;
        }
    }
}
