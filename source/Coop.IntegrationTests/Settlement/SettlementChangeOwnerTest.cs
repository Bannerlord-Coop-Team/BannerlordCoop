using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;

namespace Coop.IntegrationTests.Settlement
{
    public class SettlementChangeOwnerTest
    {
        internal TestEnvironment TestEnvironment { get; }

        public SettlementChangeOwnerTest()
        {
            // Creates a test environment with 1 server and 2 clients by default
            TestEnvironment = new TestEnvironment();
        }

        /// <summary>
        /// Verify sending StartSettlementEncounterAttempted on one client
        /// Triggers PartyEnterSettlement on all other clients
        /// </summary>
        [Fact]
        public void SettlementChangeOwner_Publishes_AllClients()
        {
            // Arrange
            var settlementId = "Test Settlement";
            var ownerId = "Owner Id";
            var capturerId = "Capturer Id";
            var detail = 2;

            var message = new LocalSettlementOwnershipChange(settlementId, ownerId, capturerId, detail);

            var client1 = TestEnvironment.Clients.First();
            var server = TestEnvironment.Server;

            // Act
            client1.SimulateMessage(this, message);

            // Assert
            // Verify the server sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ChangeSettlementOwnership>());

            // Verify the origin client sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ChangeSettlementOwnership>());

            // Verify the other clients send a single message to their game interfaces to change owner settlement
            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementOwnership>());
            }
            
        }
    }
}