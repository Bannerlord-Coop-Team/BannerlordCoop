using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Messages;
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

        public TimeHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<TimeSpeedChanged>(Handle_TimeSpeedChanged);
            messageBroker.Subscribe<NetworkTimeSpeedChanged>(Handle_NetworkTimeSpeedChanged);

            messageBroker.Subscribe<NetworkEnableTimeControls>(Handle_NetworkEnableTimeControls);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<TimeSpeedChanged>(Handle_TimeSpeedChanged);
            messageBroker.Unsubscribe<NetworkTimeSpeedChanged>(Handle_NetworkTimeSpeedChanged);

            messageBroker.Unsubscribe<NetworkEnableTimeControls>(Handle_NetworkEnableTimeControls);
        }

        internal void Handle_TimeSpeedChanged(MessagePayload<TimeSpeedChanged> obj)
        {
            var newMode = obj.What.NewControlMode;

            Logger.Verbose("Client changing time to {mode} from server", newMode);

            var payload = new NetworkRequestTimeSpeedChange(newMode);
            network.SendAll(payload);
        }

        internal void Handle_NetworkTimeSpeedChanged(MessagePayload<NetworkTimeSpeedChanged> obj)
        {
            var newMode = obj.What.NewControlMode;

            Logger.Verbose("Client requesting time change to {mode}", newMode);

            messageBroker.Publish(this, new SetTimeControlMode(newMode));
        }

        internal void Handle_NetworkEnableTimeControls(MessagePayload<NetworkEnableTimeControls> obj)
        {
            messageBroker.Publish(this, new EnableGameTimeControls());
        }
    }
}
