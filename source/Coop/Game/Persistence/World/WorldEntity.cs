using System;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.World
{
    public class WorldEntityClient : RailEntityClient<WorldState>
    {
        private readonly IEnvironment m_Environment;

        public WorldEntityClient(IEnvironment environment)
        {
            m_Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        protected override void UpdateProxy()
        {
            m_Environment.TimeControlMode = State.TimeControlMode;
            if (m_Environment.RequestedTimeControlMode.HasValue)
            {
                CampaignTimeControlMode requestedValue =
                    m_Environment.RequestedTimeControlMode.Value;
                if (requestedValue != State.TimeControlMode)
                {
                    WorldEventTimeControl evnt = EventCreator.CreateEvent<WorldEventTimeControl>();
                    evnt.RequestedTimeControlMode = requestedValue;
                    Room.RaiseEvent(evnt);
                }

                m_Environment.RequestedTimeControlMode = null;
            }
        }
    }

    public class WorldEntityServer : RailEntityServer<WorldState>
    {
        private readonly IEnvironment m_Environment;

        public WorldEntityServer(IEnvironment environment)
        {
            m_Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        protected override void UpdateAuthoritative()
        {
            State.TimeControlMode = m_Environment.TimeControlMode;
        }
    }
}
