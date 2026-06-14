using Coop.Core.Server.Services.Stances.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Stances.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Coop.IntegrationTests.Stances
{
    /// <summary>
    /// Test class for war declaration stance sync.
    /// </summary>
    public class FactionDeclareWarSyncTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Tests that a server-side war declaration replicates to all clients.
        /// </summary>
        [Fact]
        public void ServerFaction_DeclareWar_Publishes_AllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;
            var kingdom1 = server.CreateRegisteredObject<Kingdom>("kingdom1");
            var kingdom2 = server.CreateRegisteredObject<Kingdom>("kingdom2");

            var triggerMessage = new FactionWarDeclared(kingdom1, kingdom2, (int)DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision);

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to the network
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkDeclareWar>());

            // Verify each client republishes a single command to its game interface
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<DeclareWarChanged>());
            }
        }
    }
}
