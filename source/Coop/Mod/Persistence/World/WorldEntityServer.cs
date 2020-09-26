using System;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence.World
{
    /// <summary>
    ///     Singular instance representing global world state.
    /// </summary>
    public class WorldEntityServer : RailEntityServer<WorldState>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IEnvironmentServer m_Environment;

        public WorldEntityServer(IEnvironmentServer environment)
        {
            m_Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <summary>
        ///     Written when the server received change requests. Null when the server processed the requests.
        /// </summary>
        [CanBeNull] public CampaignTimeControlMode? RequestedTimeControlMode { get; set; }
        
        /// <summary>
        ///     Written when the server received change requests. Null when the server processed the requests.
        /// </summary>
        [CanBeNull] public bool? RequestedTimeControlModeLock { get; set; }

        /// <summary>
        ///     Updates the authoritative world state
        /// </summary>
        protected override void UpdateAuthoritative()
        {
            State.CampaignTimeTicks = CampaignTime.Now.GetNumTicks();

            if (!RequestedTimeControlMode.HasValue && !RequestedTimeControlModeLock.HasValue)
            {
                // No pending requests
                return;
            }

            if (!m_Environment.CanChangeTimeControlMode)
            {
                RequestedTimeControlMode = null;
                RequestedTimeControlModeLock = null;
                Logger.Trace(
                    "Time control request ignored: Cannot change time control mode right now.");
                return;
            }

            Logger.Trace("Changing time control to {request}.", RequestedTimeControlMode.Value);
            State.TimeControl = RequestedTimeControlMode.Value;
            State.TimeControlLock = RequestedTimeControlModeLock.Value;
            RequestedTimeControlMode = null;
            RequestedTimeControlModeLock = null;
        }

        public override string ToString()
        {
            return $"World ({Id}): {State}.";
        }
    }
}
