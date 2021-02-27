using System;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using RemoteAction;

namespace Coop.Mod.Persistence.World
{
    /// <summary>
    ///     Singular instance representing global world state.
    /// </summary>
    public class WorldEntityClient : RailEntityClient<WorldState>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        [NotNull] private readonly IEnvironmentClient m_Environment;

        public WorldEntityClient(IEnvironmentClient environment)
        {
            m_Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        protected override void OnPostUpdate()
        {
            base.OnPostUpdate();
            m_Environment.AuthoritativeTime = Extensions.CreateCampaignTime(State.CampaignTimeTicks);
        }
        public override string ToString()
        {
            return $"World ({Id}): {State}.";
        }
    }
}
