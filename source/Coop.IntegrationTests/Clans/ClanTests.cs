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
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ClanNameChanged>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ClanNameChanged>());

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
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ClanKingdomChanged>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ClanKingdomChanged>());

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
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ClanDestroyed>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ClanDestroyed>());

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
            Assert.Equal(1, server.InternalMessages.GetMessageCount<CompanionAdded>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<CompanionAdded>());

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
            Assert.Equal(1, server.InternalMessages.GetMessageCount<RenownAdded>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<RenownAdded>());

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
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ClanLeaderChanged>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ClanLeaderChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ClanLeaderChanged>());
            }
        }
        /// <summary>
        /// Verify sending ChangeClanInfluence on one client
        /// Triggers ClanInfluenceChanged on all other clients
        /// </summary>
        [Fact]
        public void ClanInfluenceChange_Publishes_AllClients()
        {
            // Arrange
            var clanId = "TestClan";

            var message = new ChangeClanInfluence(clanId, 50);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ClanInfluenceChanged>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ClanInfluenceChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ClanInfluenceChanged>());
            }
        }
        /// <summary>
        /// Verify sending AdoptHero on one client
        /// Triggers HeroAdopted on all other clients
        /// </summary>
        [Fact]
        public void AdoptHero_Publishes_AllClients()
        {
            // Arrange
            var clanId = "TestClan";
            var adoptedHeroId = "Adopted";
            var heroId = "PlayerHero";

            var message = new AdoptHero(adoptedHeroId, clanId, heroId);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<HeroAdopted>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<HeroAdopted>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<HeroAdopted>());
            }
        }
    }
}
