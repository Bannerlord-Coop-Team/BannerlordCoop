using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Client.Services.MobileParties.Messages.Fields;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Fields.Commands;
using GameInterface.Services.MobileParties.Messages.Fields.Events;
using GameInterface.Services.MobileParties.Patches;
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

        //Every property change uses the same messages thus one test will show same result as all others
        [Fact]
        public void ServerReceivedArmyChanged()
        {
            var triggerMessage = new MobilePartyPropertyChanged(PropertyType.Army, "differentId", "testId");

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkMobilePartyPropertyChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeMobilePartyProperty>());
            }
        }
    }
}
