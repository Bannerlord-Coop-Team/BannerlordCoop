using RailgunNet.Logic.State;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.World
{
    public class WorldState
    {
        public CampaignTimeControlMode TimeControlMode
        {
            get => (CampaignTimeControlMode) m_TimeControlMode;
            set => m_TimeControlMode = (byte) value;
        }

        #region synced data
        [Mutable] private byte m_TimeControlMode { get; set; }
        #endregion
    }
}
