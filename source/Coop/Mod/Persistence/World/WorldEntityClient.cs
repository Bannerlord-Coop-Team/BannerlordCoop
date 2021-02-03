using System;
using System.ComponentModel;
using Coop.Mod.Patch;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using RemoteAction;
using Sync.Behaviour;
using TaleWorlds.CampaignSystem;

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
        
        /// <summary>
        ///     Called when the world entity was added to the Railgun room.
        /// </summary>
        protected override void OnAdded()
        {
            State.PropertyChanged += State_PropertyChanged;
        }

        /// <summary>
        ///     Handler when any property in the world state object was changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void State_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(State.CampaignTimeTicks):
                    m_Environment.AuthoritativeTime =
                        Extensions.CreateCampaignTime(State.CampaignTimeTicks);
                    break;
            }
        }

        /// <summary>
        ///     Called when the world entity was removed from the Railgun room.
        /// </summary>
        protected override void OnRemoved()
        {
            State.PropertyChanged -= State_PropertyChanged;
        }

        public override string ToString()
        {
            return $"World ({Id}): {State}.";
        }
    }
}
