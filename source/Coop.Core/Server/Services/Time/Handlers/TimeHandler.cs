using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Time.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Core.Server.Services.Time.Handlers
{
    public class TimeHandler : IHandler
    {
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

        private void Handle_NetworkRequestTimeSpeedChange(MessagePayload<NetworkRequestTimeSpeedChange> obj)
        {
            // TODO determine time change policy

            var newMode = obj.What.NewControlMode;
            _networkMessageBroker.Publish(this, new SetTimeControlMode(Guid.Empty, newMode));
        }

        private void Handle_TimeSpeedChanged(MessagePayload<TimeSpeedChanged> obj)
        {
            _networkMessageBroker.PublishNetworkEvent(new NetworkTimeSpeedChanged());
        }
    }
}
