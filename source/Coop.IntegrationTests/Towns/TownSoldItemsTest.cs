using Coop.Core.Server.Services.Towns.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Towns.Messages;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.IntegrationTests.Towns
{
    public class TownSoldItemsTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client recieves TownSoldItemsChanged messages.
        /// </summary>
        [Fact]
        public void ServerTownSoldItemsChanged_Publishes_AllClients()
        {
            // Arrange
            string townId = "Settlement1";
            Town.SellLog[] soldItems = new Town.SellLog[] {};
            var triggerMessage = new TownSoldItemsChanged(townId, soldItems);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to it's game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeTownSoldItems>());

            // Verify the all clients send a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeTownSoldItems>());
            }
        }
    }
}
