using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.World
{
    public class World
    {
        private readonly IEnvironment m_Environment;
        public World(IEnvironment env)
        {
            m_Environment = env;
            TimeControlMode_LastWritten = TimeControlMode;
        }

        #region synced data
        public CampaignTimeControlMode TimeControlMode_LastWritten
        {
            get;
            private set;
        }
        public CampaignTimeControlMode TimeControlMode
        {
            get => m_Environment.TimeControlMode;
            set
            {
                m_Environment.TimeControlMode = value;
                TimeControlMode_LastWritten = value;
            }
        }
        #endregion

        public void Reset()
        {
            // intentionally left blank
        }
    }
}
