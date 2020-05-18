using System;
using RailgunNet.Logic;

namespace Coop.Mod.Persistence.World
{
    public class WorldEntityServer : RailEntityServer<WorldState>
    {
        private readonly IEnvironmentServer m_Environment;

        public WorldEntityServer(IEnvironmentServer environment)
        {
            m_Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }
    }
}
