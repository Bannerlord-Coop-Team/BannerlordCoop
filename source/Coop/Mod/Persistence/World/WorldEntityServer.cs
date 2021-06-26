using System;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using RemoteAction;
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
        ///     Updates the authoritative world state
        /// </summary>
        protected override void UpdateAuthoritative()
        {
            State.CampaignTimeTicks = CampaignTime.Now.GetNumTicks();
        }

        public override string ToString()
        {
            return $"World ({Id}): {State}.";
        }
    }
}
