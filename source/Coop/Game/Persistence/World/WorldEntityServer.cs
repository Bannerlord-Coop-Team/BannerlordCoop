using System;
using RailgunNet.Logic;

namespace Coop.Game.Persistence.World
{
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
