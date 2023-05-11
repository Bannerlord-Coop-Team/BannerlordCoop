using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Messages;
using Serilog;
using System;

namespace Coop.Core.Server.Services.Time.Handlers
{
    public class TimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TimeHandler>();

        private readonly INetworkMessageBroker _networkMessageBroker;

        public TimeHandler(INetworkMessageBroker messageBroker)
        {
            _networkMessageBroker = messageBroker;
            
            _networkMessageBroker.Subscribe<TimeSpeedChanged>(Handle_TimeSpeedChanged);
            _networkMessageBroker.Subscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);
        }

        public void Dispose()
        {
            _networkMessageBroker.Unsubscribe<TimeSpeedChanged>(Handle_TimeSpeedChanged);
            _networkMessageBroker.Unsubscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);
        }

        internal void Handle_NetworkRequestTimeSpeedChange(MessagePayload<NetworkRequestTimeSpeedChange> obj)
        {
            var newMode = obj.What.NewControlMode;

            Logger.Verbose("Server changing time to {mode} from client", newMode);

            _networkMessageBroker.Publish(this, new SetTimeControlMode(Guid.Empty, newMode));
        }

        internal void Handle_TimeSpeedChanged(MessagePayload<TimeSpeedChanged> obj)
        {
            var newMode = obj.What.NewControlMode;

            Logger.Verbose("Server sending time change to {mode} to client", newMode);

            _networkMessageBroker.PublishNetworkEvent(new NetworkTimeSpeedChanged(newMode));
        }
    }
}
