using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Locations.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;


namespace Coop.IntegrationTests.Locations
{
    public class AddLocationCharacterTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client receives AddLocationCharacter messages.
        /// </summary>
        [Fact]
        public void ServerLocationCharacterAdded_Publishes_AllClients()
        {
            // Arrange
            var locationId = "MyLocation";
            var characterId = "MyCharacter";
            var server = TestEnvironment.Server;

            var location = server.CreateRegisteredObject<Location>(locationId);
            var character = server.CreateRegisteredObject<CharacterObject>(characterId);

            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Location>(locationId);
                client.CreateRegisteredObject<CharacterObject>(characterId);
            }

            var triggerMessage = new LocationCharacterAdded(location, character, null, null, "sp_notable", null, null, 0, false, true);

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to its game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkAddLocationCharacter>());

            // Verify all clients receive a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<NetworkAddLocationCharacter>());
            }
        }
    }
}
