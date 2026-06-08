using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
using Serilog;
using System;

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
        private bool timeControlsLocked;
        private int loadingPlayers;

        public TimeHandler(IMessageBroker messageBroker, INetwork network, ITimeControlInterface timeControlInterface)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.timeControlInterface = timeControlInterface;
            this.timeControlInterface.AddUnpausePolicy(TimeControlLockPolicy);
            messageBroker.Subscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
            messageBroker.Subscribe<NetworkChangeTimeControlMode>(Handle_NetworkTimeSpeedChanged);
            messageBroker.Subscribe<NetworkTimeControlLockChanged>(Handle_NetworkTimeControlLockChanged);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
            messageBroker.Unsubscribe<NetworkChangeTimeControlMode>(Handle_NetworkTimeSpeedChanged);
            messageBroker.Unsubscribe<NetworkTimeControlLockChanged>(Handle_NetworkTimeControlLockChanged);
            timeControlInterface.RemoveUnpausePolicy(TimeControlLockPolicy);
        }

        internal void Handle_TimeSpeedChanged(MessagePayload<TimeSpeedChangedAttempted> obj)
        {
            var newMode = obj.What.NewControlMode;

            if (timeControlsLocked && newMode != TimeControlEnum.Pause)
            {
                messageBroker.Publish(this, new SendInformationMessage(LoadingMessage()));
                return;
            }

            Logger.Verbose("Client changing time to {mode} from server", newMode);

            var payload = new NetworkRequestTimeSpeedChange(newMode);
            network.SendAll(payload);
        }

        internal void Handle_NetworkTimeSpeedChanged(MessagePayload<NetworkChangeTimeControlMode> obj)
        {
            var newMode = obj.What.NewControlMode;

            Logger.Verbose("Client requesting time change to {mode}", newMode);

            timeControlInterface.ClientSetTimeControl(newMode);
        }

        internal void Handle_NetworkTimeControlLockChanged(MessagePayload<NetworkTimeControlLockChanged> obj)
        {
            timeControlsLocked = obj.What.IsLocked;
            loadingPlayers = timeControlsLocked ? Math.Max(1, obj.What.LoadingPlayers) : 0;

            if (timeControlsLocked)
            {
                timeControlInterface.ClientSetTimeControl(TimeControlEnum.Pause);
            }
        }

        private bool TimeControlLockPolicy()
        {
            return timeControlsLocked == false;
        }

        private string LoadingMessage()
        {
            return "Time controls disabled, " + loadingPlayers + " player(s) are currently joining the game";
        }
    }
}
