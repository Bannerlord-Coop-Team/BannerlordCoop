using Coop.Core.Server.Services.Kingdoms.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;

using TaleWorlds.CampaignSystem;
namespace Coop.IntegrationTests.Kingdoms
{
    /// <summary>
    /// Test class for NetworkRemoveDecision message handling.
    /// </summary>
    public class KingdomRemoveDecisionTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client recieves NetworkRemoveDecision messages.
        /// </summary>
        [Fact]
        public void ServerKingdom_RemoveDecision_Publishes_AllClients()
        {
            // Arrange
            var kingdom = TestEnvironment.Server.CreateRegisteredObject<Kingdom>("kingdom1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Kingdom>("kingdom1");
            }

            var triggerMessage = new DecisionRemoved(kingdom, 1);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to it's game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkRemoveDecision>());

            // Verify the all clients send a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<RemoveDecision>());
            }
        }
    }
}
