using System;
using System.ComponentModel;
using Coop.Mod.Patch;
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
    public class WorldEntityClient : RailEntityClient<WorldState>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        [NotNull] private readonly IEnvironmentClient m_Environment;

        public WorldEntityClient(IEnvironmentClient environment)
        {
            m_Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <summary>
        ///     Called to request a change to the time control mode on the server.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="value"></param>
        /// <exception cref="ArgumentException"></exception>
        private bool RequestTimeControlChange(object instance, object value)
        {
            if (!(value is CampaignTimeControlMode mode))
            {
                throw new ArgumentException(nameof(value));
            }

            if (!TimeControl.CanSyncTimeControlMode)
            {
                return false;
            }

            bool modelock = m_Environment.GetCurrentCampaign().TimeControlModeLock;

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
            TimeControl.CanSyncTimeControlMode = false;
            return false;
        }

        /// <summary>
        ///     Called to request a change to the time control lock on the server.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="value"></param>
        /// <exception cref="ArgumentException"></exception>
        private bool RequestTimeControlLockChange(object instance, object value)
        {
            if (!(value is bool modelock))
            {
                throw new ArgumentException(nameof(value));
            }

            CampaignTimeControlMode mode = m_Environment.GetCurrentCampaign().TimeControlMode;

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
            return false;
        }

        /// <summary>
        ///     Called when the world entity was added to the Railgun room.
        /// </summary>
        protected override void OnAdded()
        {
            m_Environment.TimeControlMode.SetGlobalHandler(RequestTimeControlChange);
            m_Environment.TimeControlModeLock.SetGlobalHandler(RequestTimeControlLockChange);
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
                case nameof(State.TimeControl):
                    Logger.Trace(
                        "[{tick}] Received time control mode change to '{mode}'.",
                        Room.Tick,
                        State.TimeControl);

                    m_Environment.TimeControlMode.SetTyped(
                        m_Environment.GetCurrentCampaign(),
                        State.TimeControl);
                    break;
                case nameof(State.TimeControlLock):
                    Logger.Trace(
                        "[{tick}] Received time control lock change to '{lock}'.",
                        Room.Tick,
                        State.TimeControlLock);

                    m_Environment.TimeControlModeLock.SetTyped(
                        m_Environment.GetCurrentCampaign(),
                        State.TimeControlLock);
                    break;
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
            m_Environment.TargetPosition.RemoveGlobalHandler();
            State.PropertyChanged -= State_PropertyChanged;
        }

        public override string ToString()
        {
            return $"World ({Id}): {State}.";
        }
    }
}
