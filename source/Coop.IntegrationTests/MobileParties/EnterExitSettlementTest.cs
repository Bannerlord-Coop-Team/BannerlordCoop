using Coop.IntegrationTests.Environment;
using GameInterface.Services.MobileParties.Messages.Behavior;

namespace Coop.IntegrationTests.MobileParties
{
    public class EnterExitSettlementTest
    {
        internal TestEnvironment TestEnvironment { get; }

        public EnterExitSettlementTest()
        {
            // Creates a test environment with 1 server and 2 clients by default
            TestEnvironment = new TestEnvironment();
        }

        /// <summary>
        /// Verify sending StartSettlementEncounterAttempted on one client
        /// Triggers PartyEnterSettlement on all other clients
        /// </summary>
        [Fact]
        public void EnterSettlement_Publishes_AllClients()
        {
            // Arrange
            var partyId = "Test Party";
            var settlementId = "Test Settlement";

            var message = new StartSettlementEncounterAttempted(partyId, settlementId);

            var client1 = TestEnvironment.Clients.First();

            // Act
            client1.SendMessageInternal(this, message);

            int i = 1;

            Assert.Equal(1, i);

            // Assert
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<PartyEnterSettlement>());
            }
        }

        /// <summary>
        /// Verify sending StartSettlementEncounterAttempted on one client
        /// Triggers PartyLeaveSettlement on all other clients
        /// </summary>
        [Fact]
        public void LeaveSettlement_Publishes_AllClients()
        {
            // Arrange
            var partyId = "Test Party";

            var message = new EndSettlementEncounterAttempted(partyId);

            var client1 = TestEnvironment.Clients.First();

            // Act
            client1.SendMessageInternal(this, message);

            // Assert
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<PartyLeaveSettlement>());
            }
        }
    }
}
