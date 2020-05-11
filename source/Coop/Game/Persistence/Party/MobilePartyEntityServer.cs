using JetBrains.Annotations;
using RailgunNet.Logic;

namespace Coop.Game.Persistence.Party
{
    public class MobilePartyEntityServer : RailEntityServer<MobilePartyState>
    {
        [NotNull] private readonly IEnvironmentServer m_Environment;

        public MobilePartyEntityServer([NotNull] IEnvironmentServer environment)
        {
            m_Environment = environment;
        }
    }
}
