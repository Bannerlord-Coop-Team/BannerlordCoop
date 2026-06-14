using Coop.Core.Server.Services.Stances.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Stances.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Coop.IntegrationTests.Stances
{
    /// <summary>
    /// Test class for make-peace stance sync, including daily tribute.
    /// </summary>
    public class FactionMakePeaceSyncTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Tests that a server-side peace settlement replicates to all clients.
        /// </summary>
        [Fact]
        public void ServerFaction_MakePeace_Publishes_AllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;
            var kingdom1 = server.CreateRegisteredObject<Kingdom>("kingdom1");
            var kingdom2 = server.CreateRegisteredObject<Kingdom>("kingdom2");

            var triggerMessage = new FactionPeaceMade(kingdom1, kingdom2, dailyTribute: 50, dailyTributeDuration: 30, detail: (int)MakePeaceAction.MakePeaceDetail.ByKingdomDecision);

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to the network
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkMakePeace>());

            // Verify each client republishes a single command to its game interface
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<MakePeaceChanged>());
            }
        }
    }
}
