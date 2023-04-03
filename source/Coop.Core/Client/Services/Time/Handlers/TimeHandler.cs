using Common.Messaging;
using Common.Network;
using GameInterface.Services.Time.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Core.Client.Services.Time.Handlers
{
    public class TimeHandler
    {
        private readonly INetworkMessageBroker networkMessageBroker;

        public TimeHandler(INetworkMessageBroker networkMessageBroker)
        {
            this.networkMessageBroker = networkMessageBroker;

            networkMessageBroker.Subscribe<TimeSpeedChanged>(Handle_TimeSpeedChanged);
        }

        private void Handle_TimeSpeedChanged(MessagePayload<TimeSpeedChanged> obj)
        {
            throw new NotImplementedException();
        }
    }
}
