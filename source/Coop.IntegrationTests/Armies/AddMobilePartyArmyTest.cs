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
            string mobilePartyId = "vassal_v2"; 
            string leaderMobilePartyId = "lord_v1";
            var triggerMessage = new MobilePartyInArmyAdded(mobilePartyId, leaderMobilePartyId);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to its game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkAddMobilePartyInArmy>());

            //Verify if the server is sending the same mobilePartyId and leaderMobilePartyId value
            Assert.Equal(mobilePartyId, server.NetworkSentMessages.GetMessages<NetworkAddMobilePartyInArmy>().First().MobilePartyId);
            Assert.Equal(leaderMobilePartyId, server.NetworkSentMessages.GetMessages<NetworkAddMobilePartyInArmy>().First().LeaderMobilePartyId);


            // Verify all clients receive a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<AddMobilePartyInArmy>());
            }

            // Verify all clients receive the same mobilePartyId and leaderMobilePartyId value
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(mobilePartyId, client.InternalMessages.GetMessages<AddMobilePartyInArmy>().First().MobilePartyId);
                Assert.Equal(leaderMobilePartyId, client.InternalMessages.GetMessages<AddMobilePartyInArmy>().First().LeaderMobilePartyId);
            }


        }
    }
}
