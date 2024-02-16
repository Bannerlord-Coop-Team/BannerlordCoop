using Coop.Core.Client.Services.Armies.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Armies.Data;
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
            var mobilePartyId = "MobileParty_1";
            var armyId = "CoopArmy_1";

            var data = new ArmyAddPartyData(armyId, mobilePartyId);
            var triggerMessage = new MobilePartyInArmyAdded(data);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to its game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkAddMobilePartyInArmy>());

            //Verify if the server is sending the same mobilePartyId and leaderMobilePartyId value
            Assert.Equal(data, server.NetworkSentMessages.GetMessages<NetworkAddMobilePartyInArmy>().First().Data);

            

            // Verify all clients receive a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<AddMobilePartyInArmy>());
                Assert.Equal(data, client.InternalMessages.GetMessages<AddMobilePartyInArmy>().First().Data);
            }
        }
    }
}
