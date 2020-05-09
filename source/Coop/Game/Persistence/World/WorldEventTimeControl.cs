using JetBrains.Annotations;
using NLog;
using RailgunNet;
using RailgunNet.Logic;
using RailgunNet.Util;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.World
{
    public class WorldEventTimeControl : RailEvent
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [CanBeNull] private readonly IEnvironmentServer m_Environment;

        [OnlyIn(Component.Client)]
        public WorldEventTimeControl()
        {
        }

        [OnlyIn(Component.Server)]
        public WorldEventTimeControl(IEnvironmentServer env)
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

        [OnlyIn(Component.Server)]
        protected override void Execute(RailRoom room, RailController sender)
        {
            Logger.Trace("Time control change request to {request}.", RequestedTimeControlMode);
            if (m_Environment != null)
            {
                m_Environment.TimeControlMode = RequestedTimeControlMode;
            }
        }
    }
}
