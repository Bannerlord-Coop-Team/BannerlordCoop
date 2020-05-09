using NLog;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.World
{
    public class WorldEventTimeControl : RailEvent
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
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
            Logger.Trace("Time control change request to {request}.", RequestedTimeControlMode);
            m_Environment.TimeControlMode = RequestedTimeControlMode;
        }
    }
}
