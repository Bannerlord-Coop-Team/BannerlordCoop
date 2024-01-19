using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.MobileParties.Messages;

namespace Coop.IntegrationTests.MobileParties
{
    public class TroopSync
    {
        internal TestEnvironment TestEnvironment { get; }

        public TroopSync()
        {
            // Creates a test environment with 1 server and 2 clients by default
            TestEnvironment = new TestEnvironment();
        }

        /// <summary>
        /// Verify sending TroopIndexAdded on one client
        /// Triggers AddTroopIndex on all other clients
        /// </summary>
        [Fact]
        public void TroopIndexAdded_Publishes_AllClients()
        {
            // Arrange
            var partyId = "PartyId";
            var amount = 2;
            var prisonRoster = false;

            var message = new TroopIndexAdded(partyId, prisonRoster, 1, amount, amount, amount, false);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            client1.SimulateMessage(this, message);

            // Assert
            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<AddTroopIndex>());
            }
            
            Assert.Equal(1, server.InternalMessages.GetMessageCount<AddTroopIndex>());
        }

        /// <summary>
        /// Verify sending NewTroopAdded on one client
        /// Triggers AddNewTroop on all other clients
        /// </summary>
        [Fact]
        public void NewTroopAdded_Publishes_AllClients()
        {
            // Arrange
            var characterId = "PartyId";
            var index = 2;
            var prisonRoster = false;

            var message = new NewTroopAdded(characterId, characterId, prisonRoster, false, index);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            client1.SimulateMessage(this, message);

            // Assert
            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<AddNewTroop>());
            }

            Assert.Equal(1, server.InternalMessages.GetMessageCount<AddNewTroop>());
        }
    }
}
