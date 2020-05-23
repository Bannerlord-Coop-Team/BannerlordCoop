using System;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence.World
{
    public class WorldEntityServer : RailEntityServer<WorldState>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IEnvironmentServer m_Environment;
        [CanBeNull] private CampaignTimeControlMode? m_RequestedTimeControlMode;

        public WorldEntityServer(IEnvironmentServer environment)
        {
            m_Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        protected override void UpdateAuthoritative()
        {
            if (m_RequestedTimeControlMode.HasValue && m_Environment.CanChangeTimeControlMode)
            {
                Logger.Trace(
                    "Changing time control to {request}.",
                    m_RequestedTimeControlMode.Value);
                State.TimeControlMode = m_RequestedTimeControlMode.Value;
                m_RequestedTimeControlMode = null;
            }
        }
    }
}
