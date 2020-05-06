using Coop.Common;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.World
{
    public class WorldEventTimeControl : RailEvent
    {
        private readonly IEnvironment m_Environment;

        public WorldEventTimeControl(IEnvironment env)
        {
            m_Environment = env;
        }

        public CampaignTimeControlMode RequestedTimeControlMode
        {
            get => (CampaignTimeControlMode) m_RequestedTimeControlMode;
            set => m_RequestedTimeControlMode = (byte) value;
        }

        #region synced data
        [EventData] private byte m_RequestedTimeControlMode { get; set; }
        #endregion

        protected override void Execute(RailRoom room, RailController sender)
        {
            Log.Trace($"Time control request: {RequestedTimeControlMode}.");
            m_Environment.TimeControlMode = RequestedTimeControlMode;
        }
    }
}
