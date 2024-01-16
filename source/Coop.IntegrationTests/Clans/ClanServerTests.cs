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
    public class ClanServerTests
    {
        internal TestEnvironment TestEnvironment { get; }

        public ClanServerTests()
        {
            // Creates a test environment with 1 server and 2 clients by default
            TestEnvironment = new TestEnvironment();
        }

        /// <summary>
        /// Verify sending ClanNameChange on one client
        /// Triggers ClanNameChanged on all other clients
        /// </summary>
        [Fact]
        public void ClanNameChange_Publishes_Server()
        {
            // Arrange
            var clanId = "TestClan";
            var clanName = "RealName";
            var informalName = "FakeName";

            var message = new ClanNameChanged(clanId, clanName, informalName);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, message);

            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ChangeClanName>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ChangeClanName>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeClanName>());
            }
        }
        /// <summary>
        /// Verify sending ClanKingdomChange on one client
        /// Triggers ClanKingdomChanged on all other clients
        /// </summary>
        [Fact]
        public void ClanKingdomChange_Publishes_Server()
        {
            // Arrange
            var clanId = "TestClan";
            var kingdomId = "TestKingdom";

            var message = new ClanKingdomChanged(clanId, kingdomId, 1, 1, false, true);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, message);

            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ChangeClanKingdom>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ChangeClanKingdom>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeClanKingdom>());
            }
        }
        /// <summary>
        /// Verify sending DestroyClan on one client
        /// Triggers ClanDestroyed on all other clients
        /// </summary>
        [Fact]
        public void DestroyClan_Publishes_Server()
        {
            // Arrange
            var clanId = "TestClan";

            var message = new ClanDestroyed(clanId, 1);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, message);

            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<DestroyClan>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<DestroyClan>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<DestroyClan>());
            }
        }
        /// <summary>
        /// Verify sending AddCompanion on one client
        /// Triggers CompanionAdded on all other clients
        /// </summary>
        [Fact]
        public void AddCompanion_Publishes_Server()
        {
            // Arrange
            var clanId = "TestClan";
            var heroId = "TestHero";

            var message = new CompanionAdded(clanId, heroId);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, message);
            

            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<AddCompanion>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<AddCompanion>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<AddCompanion>());
            }
        }
        /// <summary>
        /// Verify sending AddRenown on one client
        /// Triggers RenownAdded on all other clients
        /// </summary>
        [Fact]
        public void AddRenown_Publishes_Server()
        {
            // Arrange
            var clanId = "TestClan";

            var message = new ClanRenownAdded(clanId, 500, true);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, message);
            

            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<AddClanRenown>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<AddClanRenown>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<AddClanRenown>());
            }
        }
        /// <summary>
        /// Verify sending ClanleaderChange on one client
        /// Triggers ClanLeaderChanged on all other clients
        /// </summary>
        [Fact]
        public void ClanLeaderChange_Publishes_Server()
        {
            // Arrange
            var clanId = "TestClan";
            var heroId = "TestHero";

            var message = new ClanLeaderChanged(clanId, heroId);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, message);
            

            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ChangeClanLeader>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ChangeClanLeader>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeClanLeader>());
            }
        }
        /// <summary>
        /// Verify sending ChangeClanInfluence on one client
        /// Triggers ClanInfluenceChanged on all other clients
        /// </summary>
        [Fact]
        public void ClanInfluenceChange_Publishes_Server()
        {
            // Arrange
            var clanId = "TestClan";

            var message = new ClanInfluenceChanged(clanId, 50);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, message);
            

            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ChangeClanInfluence>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<ChangeClanInfluence>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeClanInfluence>());
            }
        }
        /// <summary>
        /// Verify sending AdoptHero on one client
        /// Triggers HeroAdopted on all other clients
        /// </summary>
        [Fact]
        public void AdoptHero_Publishes_Server()
        {
            // Arrange
            var clanId = "TestClan";
            var adoptedHeroId = "Adopted";
            var heroId = "PlayerHero";

            var message = new HeroAdopted(adoptedHeroId, clanId, heroId);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, message);
            
            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<AdoptHero>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<AdoptHero>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<AdoptHero>());
            }
        }
        /// <summary>
        /// Verify sending LocalNewHeir on one client
        /// Triggers NewHeirAppointed on all other clients
        /// </summary>
        [Fact]
        public void NewHeir_Publishes_Server()
        {
            // Arrange
            var playerHeroId = "TestClan";
            var heirHeroId = "Adopted";

            var message = new NewHeirAdded(heirHeroId, playerHeroId, false);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, message);
            
            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<AddNewHeir>());

            Assert.Equal(1, client1.InternalMessages.GetMessageCount<AddNewHeir>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<AddNewHeir>());
            }
        }
    }
}
