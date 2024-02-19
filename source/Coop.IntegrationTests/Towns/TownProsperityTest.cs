using Coop.Core.Server.Services.Towns.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Towns.Messages;

namespace Coop.IntegrationTests.Towns
{
    public class TownProsperityTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client recieves TownProsperityChanged messages.
        /// </summary>
        [Fact]
        public void ServerTownProsperityChanged_Publishes_AllClients()
        {
            // Arrange
            string townId = "Settlement1";
            float prosperity = 3500.0f;
            var triggerMessage = new TownProsperityChanged(townId, prosperity);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to it's game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeTownProsperity>());

            // Verify the all clients send a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeTownProsperity>());
            }
        }
    }
}
