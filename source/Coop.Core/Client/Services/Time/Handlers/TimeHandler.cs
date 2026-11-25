using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Configuration;
using GameInterface.Services.AdvancedStart.Interfaces;
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

        private const string HostOnlyTimeControlsMessage = "Only the host can pause or change the game speed on this server.";

        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ITimeControlInterface timeControlInterface;
        private readonly IAdvancedStartInterface advancedStartInterface;
        private MapEventFastForwardState mapEventState = MapEventFastForwardState.NotBlocked;

        public TimeHandler(IMessageBroker messageBroker, INetwork network, ITimeControlInterface timeControlInterface,
            IAdvancedStartInterface advancedStartInterface)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.timeControlInterface = timeControlInterface;
            this.advancedStartInterface = advancedStartInterface;
            messageBroker.Subscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
            messageBroker.Subscribe<NetworkChangeTimeControlMode>(Handle_NetworkTimeSpeedChanged);
            messageBroker.Subscribe<NetworkMapEventLockChanged>(Handle_NetworkMapEventLockChanged);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
            messageBroker.Unsubscribe<NetworkChangeTimeControlMode>(Handle_NetworkTimeSpeedChanged);
            messageBroker.Unsubscribe<NetworkMapEventLockChanged>(Handle_NetworkMapEventLockChanged);
        }

        internal void Handle_TimeSpeedChanged(MessagePayload<TimeSpeedChangedAttempted> obj)
        {
            var newMode = obj.What.NewControlMode;

            // Nothing to revert here, the time buttons show the server's mode again on the next tick
            if (!ModConfigProvider.ModOptions.AllowClientTimeControls)
            {
                messageBroker.Publish(this, new SendInformationMessage(HostOnlyTimeControlsMessage));
                return;
            }

            if (mapEventState.IsBlocked && newMode == TimeControlEnum.Play_2x)
            {
                messageBroker.Publish(this, new SendInformationMessage(mapEventState.BlockedMessage));
                return;
            }

            Logger.Verbose("Client changing time to {mode} from server", newMode);

            var payload = new NetworkRequestTimeSpeedChange(newMode);
            // The first resume locks the world-changing starts for good, so the player confirms it.
            if (newMode != TimeControlEnum.Pause && advancedStartInterface.ResumeLocksWorldStarts())
            {
                advancedStartInterface.ConfirmFirstResume(() => network.SendAll(payload));
                return;
            }

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
