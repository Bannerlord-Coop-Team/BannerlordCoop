using Coop.IntegrationTests.Environment;
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

            // Act
            client1.SendMessageInternal(this, message);

            // Assert
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementOwnership>());
            }
        }
    }
}