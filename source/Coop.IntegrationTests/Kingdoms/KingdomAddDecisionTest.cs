using Coop.Core.Server.Services.Kingdoms.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;

using TaleWorlds.CampaignSystem;
namespace Coop.IntegrationTests.Kingdoms
{
    /// <summary>
    /// Test class for NetworkAddDecision message handling.
    /// </summary>
    public class KingdomAddDecisionTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client recieves NetworkAddDecision messages.
        /// </summary>
        [Fact]
        public void ServerKingdom_AddDecision_Publishes_AllClients()
        {
            // Arrange
            var kingdom = TestEnvironment.Server.CreateRegisteredObject<Kingdom>("kingdom1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Kingdom>("kingdom1");
            }

            // The clients' GameInterface KingdomHandler dereferences Data, so it must be a
            // concrete KingdomDecisionData. The clan ids are unregistered on purpose: the
            // handler resolves them, fails gracefully, and the AddDecision message has
            // already been counted by then.
            var data = new ExpelClanFromKingdomDecisionData(
                "clan1", "kingdom1", 0, false, false, false, "clan1", "kingdom1");

            var triggerMessage = new DecisionAdded(kingdom, data, false, 0.5f);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to it's game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkAddDecision>());

            // Verify the all clients send a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<AddDecision>());
            }
        }

        [Fact]
        public void ClientKingdom_AddDecision_Publishes_ServerCommand()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();
            var server = TestEnvironment.Server;
            var kingdom = client1.CreateRegisteredObject<Kingdom>("kingdom1");
            server.CreateRegisteredObject<Kingdom>("kingdom1");

            var data = new ExpelClanFromKingdomDecisionData(
                "clan1", "kingdom1", 0, false, false, false, "clan1", "kingdom1");

            var triggerMessage = new DecisionAdded(kingdom, data, false, 0f);

            // Act
            client1.SimulateMessage(this, triggerMessage);

            // Assert
            Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<NetworkAddDecision>());
            Assert.Equal(1, server.InternalMessages.GetMessageCount<NetworkAddDecision>());
            Assert.Equal(1, server.InternalMessages.GetMessageCount<AddDecision>());
            Assert.Equal(0, client1.InternalMessages.GetMessageCount<AddDecision>());
            Assert.Equal(1, TestEnvironment.Clients.Skip(1).First().InternalMessages.GetMessageCount<AddDecision>());
        }
    }
}
