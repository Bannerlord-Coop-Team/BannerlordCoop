using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;

namespace Coop.IntegrationTests.Kingdoms
{
    public class KingdomRemoveDecisionTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client recieves RemoveDecision messages.
        /// </summary>
        [Fact]
        public void ServerKingdom_RemoveDecision_Publishes_AllClients()
        {
            // Arrange
            var triggerMessage = new LocalDecisionRemoved("Kingdom1", 5);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to it's game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<RemoveDecision>());

            // Verify the all clients send a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<RemoveDecision>());
            }
        }
    }
}
