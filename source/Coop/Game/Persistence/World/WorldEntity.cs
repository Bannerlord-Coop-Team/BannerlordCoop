using System;
using RailgunNet.Logic;

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
