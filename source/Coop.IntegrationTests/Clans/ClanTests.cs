using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Clans.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.IntegrationTests.Clans
{
    public class ClanTests
    {
        internal TestEnvironment TestEnvironment { get; }

        public ClanTests()
        {
            // Creates a test environment with 1 server and 2 clients by default
            TestEnvironment = new TestEnvironment();
        }

        /// <summary>
        /// Verify sending ClanNameChange on one client
        /// Triggers ClanNameChanged on all other clients
        /// </summary>
        [Fact]
        public void ClanNameChange_Publishes_AllClients()
        {
            // Arrange
            var clanId = "TestClan";
            var clanName = "RealName";
            var informalName = "FakeName";

            var message = new ClanNameChange(clanId, clanName, informalName);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            // Verify the server sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ClanNameChanged>());

            // Verify the origin client sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ClanNameChanged>());

            // Verify the other clients send a single message to their game interfaces to change owner settlement
            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ClanNameChanged>());
            }
        }
        /// <summary>
        /// Verify sending ClanKingdomChange on one client
        /// Triggers ClanKingdomChanged on all other clients
        /// </summary>
        [Fact]
        public void ClanKingdomChange_Publishes_AllClients()
        {
            // Arrange
            var clanId = "TestClan";
            var kingdomId = "TestKingdom";

            var message = new ClanKingdomChange(clanId, kingdomId, 1, 1, false, true);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            // Verify the server sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ClanKingdomChanged>());

            // Verify the origin client sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ClanKingdomChanged>());

            // Verify the other clients send a single message to their game interfaces to change owner settlement
            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ClanKingdomChanged>());
            }
        }
        /// <summary>
        /// Verify sending DestroyClan on one client
        /// Triggers ClanDestroyed on all other clients
        /// </summary>
        [Fact]
        public void DestroyClan_Publishes_AllClients()
        {
            // Arrange
            var clanId = "TestClan";

            var message = new DestroyClan(clanId, 1);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            // Verify the server sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ClanDestroyed>());

            // Verify the origin client sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ClanDestroyed>());

            // Verify the other clients send a single message to their game interfaces to change owner settlement
            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ClanDestroyed>());
            }
        }
        /// <summary>
        /// Verify sending AddCompanion on one client
        /// Triggers CompanionAdded on all other clients
        /// </summary>
        [Fact]
        public void AddCompanion_Publishes_AllClients()
        {
            // Arrange
            var clanId = "TestClan";
            var heroId = "TestHero";

            var message = new AddCompanion(clanId, heroId);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            // Verify the server sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, server.InternalMessages.GetMessageCount<CompanionAdded>());

            // Verify the origin client sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, client1.InternalMessages.GetMessageCount<CompanionAdded>());

            // Verify the other clients send a single message to their game interfaces to change owner settlement
            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<CompanionAdded>());
            }
        }
        /// <summary>
        /// Verify sending AddRenown on one client
        /// Triggers RenownAdded on all other clients
        /// </summary>
        [Fact]
        public void AddRenown_Publishes_AllClients()
        {
            // Arrange
            var clanId = "TestClan";

            var message = new AddRenown(clanId, 500, true);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            // Verify the server sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, server.InternalMessages.GetMessageCount<RenownAdded>());

            // Verify the origin client sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, client1.InternalMessages.GetMessageCount<RenownAdded>());

            // Verify the other clients send a single message to their game interfaces to change owner settlement
            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<RenownAdded>());
            }
        }
        /// <summary>
        /// Verify sending ClanleaderChange on one client
        /// Triggers ClanLeaderChanged on all other clients
        /// </summary>
        [Fact]
        public void ClanLeaderChange_Publishes_AllClients()
        {
            // Arrange
            var clanId = "TestClan";
            var heroId = "TestHero";

            var message = new ClanLeaderChange(clanId, heroId);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            // Verify the server sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ClanLeaderChanged>());

            // Verify the origin client sends a single message to it's game interface to change owner settlement
            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ClanLeaderChanged>());

            // Verify the other clients send a single message to their game interfaces to change owner settlement
            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ClanLeaderChanged>());
            }
        }
    }
}
