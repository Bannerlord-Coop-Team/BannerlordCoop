using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.MapEvents.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.IntegrationTests.MapEvents
{
    public class MapEventTests
    {
        internal TestEnvironment TestEnvironment { get; }

        public MapEventTests()
        {
            // Creates a test environment with 1 server and 2 clients by default
            TestEnvironment = new TestEnvironment();
        }

        /// <summary>
        /// Verify sending BattleStarted on one client
        /// Triggers StartBattle on all other clients
        /// </summary>
        [Fact]
        public void BattleStarted_Publishes_AllClients()
        {
            // Arrange
            var attackerPartyId = "AttackerClan";
            var defenderPartyId = "DefenderClan";

            var message = new BattleStarted(attackerPartyId, defenderPartyId);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<StartBattle>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<StartBattle>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<StartBattle>());
            }
        }

        /// <summary>
        /// Verify sending BattleEnded on one client
        /// Triggers EndBattle on all other clients
        /// </summary>
        [Fact]
        public void BattleEnded_Publishes_AllClients()
        {
            // Arrange
            var partyId = "AttackerClan";

            var message = new BattleEnded(partyId);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<EndBattle>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<EndBattle>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<EndBattle>());
            }
        }
    }
}
