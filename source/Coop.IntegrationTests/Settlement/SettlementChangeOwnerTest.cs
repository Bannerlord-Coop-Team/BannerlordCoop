using Coop.Core.Client.Services.Settlements.Messages;
using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;

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

            var message = new SettlementOwnershipChangeRequest(settlementId, ownerId, capturerId, detail);

            var client1 = TestEnvironment.Clients.First();
            var server = TestEnvironment.Server;

            // Act
            client1.SendMessageExternal(message);

            Console.WriteLine(TestEnvironment.Clients.Count());

            // Assert
            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<SettlementOwnershipChangeApproved>());
            }
            Assert.Equal(1, server.InternalMessages.GetMessageCount<SettlementOwnershipChangeRequest>());
        }
    }
}