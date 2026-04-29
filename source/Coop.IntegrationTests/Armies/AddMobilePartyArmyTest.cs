using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Armies.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;


namespace Coop.IntegrationTests.Armies
{
    public class AddMobilePartyArmyTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client receives AddMobilePartyInArmy messages.
        /// </summary>
        [Fact]
        public void ServerMobilePartyInArmyAdded_Publishes_AllClients()
        {
            // Arrange
            var mobilePartyId = "MyParty";
            var armyId = "MyArmy";
            var server = TestEnvironment.Server;

            var party = server.CreateRegisteredObject<MobileParty>(mobilePartyId);
            var army = server.CreateRegisteredObject<Army>(armyId);

            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<MobileParty>(mobilePartyId);
                client.CreateRegisteredObject<Army>(armyId);
            }

            var triggerMessage = new MobilePartyInArmyAdded(army, party);

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to its game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkAddMobilePartyInArmy>());

            // Verify all clients receive a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<NetworkAddMobilePartyInArmy>());
            }
        }
    }
}
