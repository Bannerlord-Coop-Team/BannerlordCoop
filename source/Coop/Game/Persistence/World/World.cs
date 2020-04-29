using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.World
{
    public class World
    {
        public World()
        {
            Reset();
        }

        #region synced data
        public CampaignTimeControlMode TimeControlMode
        {
            get => Environment.Current.TimeControlMode;
            set => Environment.Current.TimeControlMode = value;
        }
        #endregion

        public void Reset()
        {
            TimeControlMode = CampaignTimeControlMode.Stop;
        }
    }
}
