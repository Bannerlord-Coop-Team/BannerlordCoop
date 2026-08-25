using Coop.Core.Server.Services.Kingdoms.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
namespace Coop.IntegrationTests.Kingdoms
{
    /// <summary>
    /// Test class for NetworkAddDecision message handling.
    /// </summary>
    [Collection(KingdomSyncGameThreadCollection.Name)]
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
            var decision = CreateDecision(TestEnvironment.Server);
            var triggerMessage = new DecisionAdded(kingdom, decision, false, 0.5f, wasQueued: true);
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

        /// <summary>
        /// A proposal reaches the server as an add with no answer attached, and the receive handler sends
        /// nothing itself. The broadcast comes from the server's own apply, so nobody is sent two adds.
        /// </summary>
        [Fact]
        public void ClientKingdom_AddDecision_Publishes_ServerCommand()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();
            var server = TestEnvironment.Server;
            var kingdom = client1.CreateRegisteredObject<Kingdom>("kingdom1");
            server.CreateRegisteredObject<Kingdom>("kingdom1");
            var decision = CreateDecision(client1);
            var triggerMessage = new DecisionAdded(kingdom, decision, false, 0f, wasQueued: null);
            // Act
            client1.SimulateMessage(this, triggerMessage);
            // Assert
            Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<NetworkAddDecision>());
            Assert.Equal(1, server.InternalMessages.GetMessageCount<NetworkAddDecision>());
            Assert.Single(
                server.InternalMessages.GetMessages<AddDecision>(),
                message => message.WasQueued == null);
            Assert.Empty(server.NetworkSentMessages.GetMessages<NetworkAddDecision>());
        }
        /// <summary>
        /// A clan outside the kingdom still mirrors that kingdom's queue, otherwise its
        /// _unresolvedDecisions stays shorter than the server's and every later decision index is off.
        /// </summary>
        [Fact]
        public void ServerKingdom_AddDecision_Publishes_ClientWithClanOutsideKingdom()
        {
            var server = TestEnvironment.Server;
            var kingdom = server.CreateRegisteredObject<Kingdom>("kingdom1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Kingdom>("kingdom1");
            }
            var outsideClient = TestEnvironment.Clients.First();
            PlaceControllerClanOutsideKingdom(outsideClient);
            var decision = CreateDecision(server);

            server.SimulateMessage(this, new DecisionAdded(kingdom, decision, false, 0.5f, wasQueued: true));

            Assert.Equal(1, outsideClient.InternalMessages.GetMessageCount<AddDecision>());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ServerKingdom_AddDecision_Forwards_ServerQueueAnswer(bool wasQueued)
        {
            var server = TestEnvironment.Server;
            var kingdom = server.CreateRegisteredObject<Kingdom>("kingdom1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Kingdom>("kingdom1");
            }
            var decision = CreateDecision(server);

            server.SimulateMessage(this, new DecisionAdded(kingdom, decision, false, 0.5f, wasQueued));

            Assert.Single(
                server.NetworkSentMessages.GetMessages<NetworkAddDecision>(),
                message => message.WasQueued == wasQueued);
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Single(
                    client.InternalMessages.GetMessages<AddDecision>(),
                    message => message.WasQueued == wasQueued);
            }
        }

        /// <summary>
        /// A client proposal is a request, the server decides queue-vs-resolve for everyone.
        /// </summary>
        [Fact]
        public void ClientKingdom_AddDecision_DoesNotSend_LocalQueueAnswer()
        {
            var client1 = TestEnvironment.Clients.First();
            var kingdom = client1.CreateRegisteredObject<Kingdom>("kingdom1");
            TestEnvironment.Server.CreateRegisteredObject<Kingdom>("kingdom1");
            var decision = CreateDecision(client1);

            client1.SimulateMessage(this, new DecisionAdded(kingdom, decision, false, 0f, wasQueued: true));

            Assert.Single(
                client1.NetworkSentMessages.GetMessages<NetworkAddDecision>(),
                message => message.WasQueued == null);
        }

        private static void PlaceControllerClanOutsideKingdom(EnvironmentInstance client)
        {
            client.Resolve<IControllerIdProvider>().SetControllerId("player1");
            client.Resolve<IPlayerManager>().AddPlayer(
                new Player("player1", "hero1", "party1", "clan1", "character1"));
            var otherKingdom = client.CreateRegisteredObject<Kingdom>("kingdom_other");
            client.CreateRegisteredObject<Clan>("clan1")._kingdom = otherKingdom;
        }

        private static KingdomDecision CreateDecision(EnvironmentInstance instance)
        {
            instance.CreateRegisteredObject<Clan>("clan1");
            instance.CreateRegisteredObject<Kingdom>("kingdom2");
            var data = new DeclareWarDecisionData("clan1", "kingdom1", 0, false, false, false, "kingdom2");
            Assert.True(data.TryGetKingdomDecision(instance.ObjectManager, out var decision));
            return decision;
        }
    }
}
