using Coop.Core.Server.Services.Armies.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Armies.Messages;


namespace Coop.IntegrationTests.Armies
{
    public class RemoveMobilePartyArmyTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client receives RemoveMobilePartyInArmy messages.
        /// </summary>
        [Fact]
        public void ServerMobilePartyInArmyRemoved_Publishes_AllClients()
        {
            // Arrange
            string mobilePartyId = "vassal_v2";
            string leaderMobilePartyId = "lord_v1";
            var triggerMessage = new MobilePartyInArmyRemoved(mobilePartyId, leaderMobilePartyId);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to its game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeRemoveMobilePartyInArmy>());

            //Verify if the server is sending the same mobilePartyId and leaderMobilePartyId value
            Assert.Equal(mobilePartyId, server.NetworkSentMessages.GetMessages<NetworkChangeRemoveMobilePartyInArmy>().First().MobilePartyId);
            Assert.Equal(leaderMobilePartyId, server.NetworkSentMessages.GetMessages<NetworkChangeRemoveMobilePartyInArmy>().First().LeaderMobilePartyId);


            // Verify all clients receive a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<RemoveMobilePartyInArmy>());
            }

            // Verify all clients receive the same mobilePartyId and leaderMobilePartyId value
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(mobilePartyId, client.InternalMessages.GetMessages<RemoveMobilePartyInArmy>().First().MobilePartyId);
                Assert.Equal(leaderMobilePartyId, client.InternalMessages.GetMessages<RemoveMobilePartyInArmy>().First().LeaderMobilePartyId);
            }


        }
    }
}
