using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Locations.Messages;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;


namespace Coop.IntegrationTests.Locations
{
    public class AddLocationSpecialItemTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client receives AddLocationSpecialItem messages.
        /// </summary>
        [Fact]
        public void ServerLocationSpecialItemAdded_Publishes_AllClients()
        {
            // Arrange
            var locationId = "MyLocation";
            var itemId = "MyItem";
            var server = TestEnvironment.Server;

            var location = server.CreateRegisteredObject<Location>(locationId);
            var item = server.CreateRegisteredObject<ItemObject>(itemId);

            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Location>(locationId);
                client.CreateRegisteredObject<ItemObject>(itemId);
            }

            var triggerMessage = new LocationSpecialItemAdded(location, item);

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to its game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkAddLocationSpecialItem>());

            // Verify all clients receive a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<NetworkAddLocationSpecialItem>());
            }
        }
    }
}
