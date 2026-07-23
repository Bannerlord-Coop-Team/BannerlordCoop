using Coop.Core.Server.Services.Towns.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Towns.Messages;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
namespace Coop.IntegrationTests.Towns
{
    public class TownGovernorTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client recieves TownGovernorChanged messages.
        /// </summary>
        [Fact]
        public void ServerTownGovernorChanged_Publishes_AllClients()
        {
            // Arrange
            var town = TestEnvironment.Server.CreateRegisteredObject<Town>("town1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Town>("town1");
            }

            var governor = TestEnvironment.Server.CreateRegisteredObject<Hero>("governor1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Hero>("governor1");
            }

            var triggerMessage = new TownGovernorChanged(town, governor);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to it's game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeTownGovernor>());

            // Verify the all clients send a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeTownGovernor>());
            }
        }
    }
}
