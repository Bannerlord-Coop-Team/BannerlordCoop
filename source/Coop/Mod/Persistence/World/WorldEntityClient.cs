using System;
using System.ComponentModel;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence.World
{
    public class WorldEntityClient : RailEntityClient<WorldState>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [NotNull] private readonly IEnvironmentClient m_Environment;

        public WorldEntityClient(IEnvironmentClient environment)
        {
            m_Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        private void RequestTimeControlChange(object instance, object value)
        {
            if (!(value is CampaignTimeControlMode mode))
            {
                throw new ArgumentException(nameof(value));
            }

            bool modelock = this.m_Environment.GetCurrentCampaign().TimeControlModeLock;

            Logger.Trace(
                "[{tick}] Request time control mode '{mode}'.",
                Room.Tick,
                (mode, modelock));
            Room.RaiseEvent<EventTimeControl>(
                e =>
                {
                    e.EntityId = Id;
                    e.RequestedTimeControlMode = (mode, modelock);
                });
        }

        private void RequestTimeControlLockChange(object instance, object value)
        {
            if (!(value is bool modelock))
            {
                throw new ArgumentException(nameof(value));
            }

            CampaignTimeControlMode mode = this.m_Environment.GetCurrentCampaign().TimeControlMode;

            Logger.Trace(
                "[{tick}] Request time control mode '{mode}'.",
                Room.Tick,
                (mode, modelock));
            Room.RaiseEvent<EventTimeControl>(
                e =>
                {
                    e.EntityId = Id;
                    e.RequestedTimeControlMode = (mode, modelock);
                });
        }

        protected override void OnAdded()
        {
            m_Environment.TimeControlMode.SetGlobalHandler(RequestTimeControlChange);
            m_Environment.TimeControlModeLock.SetGlobalHandler(RequestTimeControlLockChange);
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

                m_Environment.TimeControlMode.SetTyped(
                    m_Environment.GetCurrentCampaign(),
                    State.TimeControlMode.Item1);

                m_Environment.TimeControlModeLock.SetTyped(
                    m_Environment.GetCurrentCampaign(),
                    State.TimeControlMode.Item2);
            }
            else if (e.PropertyName == nameof(State.CampaignTimeTicks))
            {
                m_Environment.AuthoritativeTime = Extensions.CreateCampaignTime(State.CampaignTimeTicks);
            }
        }

        protected override void OnRemoved()
        {
            m_Environment.TargetPosition.RemoveGlobalHandler();
            State.PropertyChanged -= State_PropertyChanged;
        }

        public override string ToString()
        {
            return $"World ({Id}): {State}.";
        }
    }
}
