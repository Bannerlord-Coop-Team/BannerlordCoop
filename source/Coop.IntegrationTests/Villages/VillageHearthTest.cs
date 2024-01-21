using Coop.Core.Server.Services.Villages.Messages;
using Coop.IntegrationTests.Environment.Instance;
using Coop.IntegrationTests.Environment;
using GameInterface.Services.Villages.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.IntegrationTests.Villages
{
    public class VillageHearthTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client recieves VillageHearthChanged messsages.
        /// </summary>
        [Fact]
        public void ServerVillageHearthChanged_Publishes_AllClients()
        {
            // Arrange
            string villageId = "Settlement1";
            float hearth = 1.0f;
            var triggerMessage = new VillageHearthChanged(villageId, hearth);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to it's game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeVillageHearth>());

            // Verify the all clients send a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeVillageHearth>());
            }
        }
    }
}
