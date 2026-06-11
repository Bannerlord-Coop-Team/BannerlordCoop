using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Locations.Messages;
using TaleWorlds.CampaignSystem.Settlements.Locations;


namespace Coop.IntegrationTests.Locations
{
    public class RemoveAllLocationCharactersTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client receives RemoveAllLocationCharacters messages.
        /// </summary>
        [Fact]
        public void ServerAllLocationCharactersRemoved_Publishes_AllClients()
        {
            // Arrange
            var locationId = "MyLocation";
            var server = TestEnvironment.Server;

            var location = server.CreateRegisteredObject<Location>(locationId);

            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Location>(locationId);
            }

            var triggerMessage = new AllLocationCharactersRemoved(location);

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to its game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkRemoveAllLocationCharacters>());

            // Verify all clients receive a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<NetworkRemoveAllLocationCharacters>());
            }
        }
    }
}
