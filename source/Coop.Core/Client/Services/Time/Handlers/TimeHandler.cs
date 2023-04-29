using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Time.Messages;
using Serilog;
using Serilog.Core;
using System;

namespace Coop.Core.Client.Services.Time.Handlers
{
    public class TimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TimeHandler>();

        private readonly INetworkMessageBroker networkMessageBroker;

        public TimeHandler(INetworkMessageBroker networkMessageBroker)
        {
            this.networkMessageBroker = networkMessageBroker;

            networkMessageBroker.Subscribe<TimeSpeedChanged>(Handle_TimeSpeedChanged);
            networkMessageBroker.Subscribe<NetworkTimeSpeedChanged>(Handle_NetworkTimeSpeedChanged);

            networkMessageBroker.Subscribe<NetworkEnableTimeControls>(Handle_NetworkEnableTimeControls);
        }

        public void Dispose()
        {
            networkMessageBroker.Unsubscribe<TimeSpeedChanged>(Handle_TimeSpeedChanged);
            networkMessageBroker.Unsubscribe<NetworkTimeSpeedChanged>(Handle_NetworkTimeSpeedChanged);
        }

        internal void Handle_TimeSpeedChanged(MessagePayload<TimeSpeedChanged> obj)
        {
            var newMode = obj.What.NewControlMode;

            Logger.Verbose("Client changing time to {mode} from server", newMode);

            var payload = new NetworkRequestTimeSpeedChange(newMode);
            networkMessageBroker.PublishNetworkEvent(payload);
        }

        internal void Handle_NetworkTimeSpeedChanged(MessagePayload<NetworkTimeSpeedChanged> obj)
        {
            var newMode = obj.What.NewControlMode;

            Logger.Verbose("Client requesting time change to {mode}", newMode);

            networkMessageBroker.Publish(this, new SetTimeControlMode(Guid.Empty, newMode));

            
        }

        internal void Handle_NetworkEnableTimeControls(MessagePayload<NetworkEnableTimeControls> obj)
        {
            networkMessageBroker.Publish(this, new EnableGameTimeControls());
        }
    }
}
