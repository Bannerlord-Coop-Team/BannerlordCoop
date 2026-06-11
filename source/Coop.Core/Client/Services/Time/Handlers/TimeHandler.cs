using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MapEvents;
using Serilog;

namespace Coop.Core.Client.Services.Time.Handlers
{
    /// <summary>
    /// Handles time control for the client
    /// </summary>
    public class TimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TimeHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ITimeControlInterface timeControlInterface;
        private TimeControlLockState timeControlLockState = TimeControlLockState.Unlocked;
        private MapEventFastForwardState mapEventState = MapEventFastForwardState.NotBlocked;

        public TimeHandler(IMessageBroker messageBroker, INetwork network, ITimeControlInterface timeControlInterface)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.timeControlInterface = timeControlInterface;
            this.timeControlInterface.AddUnpausePolicy(TimeControlLockPolicy);
            messageBroker.Subscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
            messageBroker.Subscribe<NetworkChangeTimeControlMode>(Handle_NetworkTimeSpeedChanged);
            messageBroker.Subscribe<NetworkTimeControlLockChanged>(Handle_NetworkTimeControlLockChanged);
            messageBroker.Subscribe<NetworkMapEventLockChanged>(Handle_NetworkMapEventLockChanged);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
            messageBroker.Unsubscribe<NetworkChangeTimeControlMode>(Handle_NetworkTimeSpeedChanged);
            messageBroker.Unsubscribe<NetworkTimeControlLockChanged>(Handle_NetworkTimeControlLockChanged);
            messageBroker.Unsubscribe<NetworkMapEventLockChanged>(Handle_NetworkMapEventLockChanged);
            timeControlInterface.RemoveUnpausePolicy(TimeControlLockPolicy);
        }

        internal void Handle_TimeSpeedChanged(MessagePayload<TimeSpeedChangedAttempted> obj)
        {
            var newMode = obj.What.NewControlMode;

            if (timeControlLockState.IsLocked && newMode != TimeControlEnum.Pause)
            {
                messageBroker.Publish(this, new SendInformationMessage(timeControlLockState.LoadingMessage));
                return;
            }

            if (mapEventState.IsBlocked && newMode == TimeControlEnum.Play_2x)
            {
                messageBroker.Publish(this, new SendInformationMessage(mapEventState.BlockedMessage));
                return;
            }

            Logger.Verbose("Client changing time to {mode} from server", newMode);

            var payload = new NetworkRequestTimeSpeedChange(newMode);
            network.SendAll(payload);
        }

        internal void Handle_NetworkMapEventLockChanged(MessagePayload<NetworkMapEventLockChanged> obj)
        {
            var wasBlocked = mapEventState.IsBlocked;
            mapEventState = MapEventFastForwardState.FromNetworkMessage(obj.What);

            if (mapEventState.IsBlocked && !wasBlocked)
            {
                messageBroker.Publish(this, new SendInformationMessage(MapEventTimeControlMessages.FastForwardDisabled));
            }
            else if (!mapEventState.IsBlocked && wasBlocked)
            {
                messageBroker.Publish(this, new SendInformationMessage(MapEventTimeControlMessages.FastForwardEnabled));
            }
        }

        internal void Handle_NetworkTimeSpeedChanged(MessagePayload<NetworkChangeTimeControlMode> obj)
        {
            var newMode = obj.What.NewControlMode;

            Logger.Verbose("Client requesting time change to {mode}", newMode);

            timeControlInterface.ClientSetTimeControl(newMode);
        }

        internal void Handle_NetworkTimeControlLockChanged(MessagePayload<NetworkTimeControlLockChanged> obj)
        {
            timeControlLockState = TimeControlLockState.FromNetworkMessage(obj.What);

            if (timeControlLockState.IsLocked)
            {
                timeControlInterface.ClientSetTimeControl(TimeControlEnum.Pause);
            }
        }

        private bool TimeControlLockPolicy()
        {
            return timeControlLockState.IsLocked == false;
        }

        private readonly struct TimeControlLockState
        {
            public static TimeControlLockState Unlocked => new TimeControlLockState(false, 0);

            public bool IsLocked { get; }
            public int LoadingPlayers { get; }
            public string LoadingMessage => "Time controls disabled, " + LoadingPlayers + " player(s) are currently joining the game";

            private TimeControlLockState(bool isLocked, int loadingPlayers)
            {
                IsLocked = isLocked;
                LoadingPlayers = loadingPlayers;
            }

            public static TimeControlLockState FromNetworkMessage(NetworkTimeControlLockChanged message)
            {
                if (message.IsLocked == false)
                {
                    return Unlocked;
                }

                return new TimeControlLockState(true, message.LoadingPlayers);
            }
        }

        private readonly struct MapEventFastForwardState
        {
            public static MapEventFastForwardState NotBlocked => new MapEventFastForwardState(0);

            public int PlayersInMapEvent { get; }
            public bool IsBlocked => PlayersInMapEvent > 0;
            public string BlockedMessage => MapEventTimeControlMessages.FastForwardBlocked(PlayersInMapEvent);

            private MapEventFastForwardState(int playersInMapEvent)
            {
                PlayersInMapEvent = playersInMapEvent;
            }

            public static MapEventFastForwardState FromNetworkMessage(NetworkMapEventLockChanged message)
            {
                return new MapEventFastForwardState(message.PlayersInMapEvent);
            }
        }
    }
}
