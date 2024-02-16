using Coop.Core.Client.Services.Armies.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Armies.Messages;


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
            List<string> mobilePartyListId = new List<string> { "MobileParty_1", "MobileParty_2" };
            string armyId = "CoopArmy_1";
            var triggerMessage = new MobilePartyInArmyAdded(mobilePartyListId, armyId);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to its game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkAddMobilePartyInArmy>());

            //Verify if the server is sending the same mobilePartyId and leaderMobilePartyId value
            Assert.Equal(mobilePartyListId, server.NetworkSentMessages.GetMessages<NetworkAddMobilePartyInArmy>().First().MobilePartyListId);
            Assert.Equal(armyId, server.NetworkSentMessages.GetMessages<NetworkAddMobilePartyInArmy>().First().ArmyId);

            

            // Verify all clients receive a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<AddMobilePartyInArmy>());
            }

            // Verify all clients receive the same mobilePartyId and leaderMobilePartyId value
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(mobilePartyListId, client.InternalMessages.GetMessages<AddMobilePartyInArmy>().First().MobilePartyListId);
                Assert.Equal(armyId, client.InternalMessages.GetMessages<AddMobilePartyInArmy>().First().ArmyId);
            }


        }
    }
}
