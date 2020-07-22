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

        public WorldEntityServer(IEnvironmentServer environment)
        {
            m_Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        [CanBeNull] public CampaignTimeControlMode? RequestedTimeControlMode { get; set; }
        [CanBeNull] public bool? RequestedTimeControlModeLock { get; set; }

        protected override void UpdateAuthoritative()
        {
            if (!RequestedTimeControlMode.HasValue && !RequestedTimeControlModeLock.HasValue)
            {
                // No pending requests
                return;
            }

            if (!m_Environment.CanChangeTimeControlMode)
            {
                Logger.Trace("Time control request ignored: Cannot change time control mode right now.");
                return;
            }
                
            Logger.Trace("Changing time control to {request}.", RequestedTimeControlMode.Value);
            State.TimeControlMode = (RequestedTimeControlMode.Value , RequestedTimeControlModeLock.Value);
            RequestedTimeControlMode = null;
            RequestedTimeControlModeLock = null;
        }

        public override string ToString()
        {
            return $"World ({Id}): {State}.";
        }
    }
}
