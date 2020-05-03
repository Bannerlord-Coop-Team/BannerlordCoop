using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.World
{
    public class World
    {
        public void Reset()
        {
            TimeControlMode = CampaignTimeControlMode.Stop;
        }
        
        #region synced data
        public CampaignTimeControlMode TimeControlMode { get; set; }
        #endregion
    }
}
