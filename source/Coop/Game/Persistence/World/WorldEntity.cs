using System;
using System.Reflection;
using NLog;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.World
{
    public class WorldEntityClient : RailEntityClient<WorldState>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IEnvironmentClient m_Environment;

        public WorldEntityClient(IEnvironmentClient environment)
        {
            m_Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        protected override void OnAdded()
        {
            if (m_Environment.TimeControlMode != null)
            {
                Logger.Error(
                    "TimeControlMode was not null indicating another WorldEntity instance exists. WorldEntity should only exist once.");
            }

            PropertyInfo position = typeof(WorldState).GetProperty(nameof(State.TimeControlMode));
            m_Environment.TimeControlMode =
                new RemoteValue<CampaignTimeControlMode>(State, position);
        }

        protected override void OnRemoved()
        {
            m_Environment.TimeControlMode = null;
        }

        protected override void UpdateProxy()
        {
            CampaignTimeControlMode? requestedMode = m_Environment.TimeControlMode?.DrainRequest();
            if (requestedMode.HasValue)
            {
                if (requestedMode.Value != State.TimeControlMode)
                {
                    Room.RaiseEvent<WorldEventTimeControl>(
                        e => { e.RequestedTimeControlMode = requestedMode.Value; });
                }
            }
        }
    }

    public class WorldEntityServer : RailEntityServer<WorldState>
    {
        private readonly IEnvironmentServer m_Environment;

        public WorldEntityServer(IEnvironmentServer environment)
        {
            m_Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        protected override void UpdateAuthoritative()
        {
            State.TimeControlMode = m_Environment.TimeControlMode;
        }
    }
}
