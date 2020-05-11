using System;
using System.ComponentModel;
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

        private void RequestTimeControlChange(object value)
        {
            if (!(value is CampaignTimeControlMode))
            {
                throw new ArgumentException(nameof(value));
            }

            Logger.Trace(
                "[{tick}] Request time control mode '{mode}'.",
                Room.Tick,
                (CampaignTimeControlMode) value);
            Room.RaiseEvent<EventTimeControl>(
                e =>
                {
                    e.RequestedTimeControlMode = (CampaignTimeControlMode) value;
                    e.EntityId = Id;
                });
        }

        protected override void OnAdded()
        {
            m_Environment.TimeControlMode.SyncHandler += RequestTimeControlChange;
            State.PropertyChanged += State_PropertyChanged;
        }

        private void State_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(State.TimeControlMode))
            {
                Logger.Trace(
                    "[{tick}] Received time controle mode change to '{mode}'.",
                    Room.Tick,
                    State.TimeControlMode);
                m_Environment.TimeControlMode.Set(
                    m_Environment.GetTimeController(),
                    State.TimeControlMode);
            }
        }

        protected override void OnRemoved()
        {
            m_Environment.TargetPosition.SyncHandler -= RequestTimeControlChange;
            State.PropertyChanged -= State_PropertyChanged;
        }
    }
}
