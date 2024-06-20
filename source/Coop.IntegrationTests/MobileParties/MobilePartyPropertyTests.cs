using Coop.Core.Client.Services.MobileParties.Messages.Fields;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.MobileParties.Messages.Fields.Commands;
using GameInterface.Services.MobileParties.Messages.Fields.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.IntegrationTests.MobileParties
{
    public class MobilePartyPropertyTests
    {
        // Creates a test environment with 1 server and 2 clients by default
        private TestEnvironment TestEnvironment { get; } = new();

        [Fact]
        public void ServerReceivedArmyChanged()
        {
            var triggerMessage = new AttachedToChanged("differentId", "testId");

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkAttachedToChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeAttachedTo>());
            }
        }
    }
}
