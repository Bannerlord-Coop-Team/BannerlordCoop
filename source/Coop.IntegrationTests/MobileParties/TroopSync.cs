using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.MobileParties.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        /// Verify sending TroopCountChanged on one client
        /// Triggers TroopCountChanged on all other clients
        /// </summary>
        [Fact]
        public void TroopCountChanged_Publishes_AllClients()
        {
            // Arrange
            var characterId = "CharacterId";
            var partyId = "PartyId";
            var amount = 2;
            var prisonRoster = false;

            var message = new TroopCountChanged(characterId, amount, partyId, prisonRoster);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<UnitRecruitGranted>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<UnitRecruitGranted>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<UnitRecruitGranted>());
            }
        }
    }
}
