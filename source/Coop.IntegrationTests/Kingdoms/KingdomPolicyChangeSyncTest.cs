using Coop.Core.Server.Services.Kingdoms.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.IntegrationTests.Kingdoms
{
    /// <summary>
    /// Test class for kingdom policy change message handling.
    /// </summary>
    [Collection(KingdomSyncGameThreadCollection.Name)]
    public class KingdomPolicyChangeSyncTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Tests that a server-side kingdom policy change replicates to all clients.
        /// </summary>
        [Fact]
        public void ServerKingdom_ChangePolicy_Publishes_AllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;
            var kingdom = server.CreateRegisteredObject<Kingdom>("kingdom1");
            var policy = server.CreateRegisteredObject<PolicyObject>("policy1");

            var triggerMessage = new KingdomPolicyChanged(kingdom, policy, isAdd: true);

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to the network
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeKingdomPolicy>());

            // Verify each client republishes a single command to its game interface
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeKingdomPolicy>());
            }
        }

        [Fact]
        public void ServerKingdom_AddPolicyAction_Publishes_AllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;
            var kingdom = server.CreateRegisteredObject<Kingdom>("kingdom1");
            var policy = server.CreateRegisteredObject<PolicyObject>("policy1");

            // Act
            server.Call(() =>
            {
                kingdom._activePolicies = new MBList<PolicyObject>();
                KingdomPatches.AddPolicyPrefix(kingdom, policy);
            });

            // Assert
            Assert.Single(server.NetworkSentMessages.GetMessages<NetworkChangeKingdomPolicy>(),
                message => message.KingdomId == "kingdom1"
                           && message.PolicyId == "policy1"
                           && message.IsAdd);

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Single(client.InternalMessages.GetMessages<ChangeKingdomPolicy>(),
                    message => message.KingdomId == "kingdom1"
                               && message.PolicyId == "policy1"
                               && message.IsAdd);
            }
        }

        [Fact]
        public void ServerKingdom_RemovePolicyAction_Publishes_AllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;
            var kingdom = server.CreateRegisteredObject<Kingdom>("kingdom1");
            var policy = server.CreateRegisteredObject<PolicyObject>("policy1");

            // Act
            server.Call(() =>
            {
                kingdom._activePolicies = new MBList<PolicyObject> { policy };
                KingdomPatches.RemovePolicyPrefix(kingdom, policy);
            });

            // Assert
            Assert.Single(server.NetworkSentMessages.GetMessages<NetworkChangeKingdomPolicy>(),
                message => message.KingdomId == "kingdom1"
                           && message.PolicyId == "policy1"
                           && !message.IsAdd);

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Single(client.InternalMessages.GetMessages<ChangeKingdomPolicy>(),
                    message => message.KingdomId == "kingdom1"
                               && message.PolicyId == "policy1"
                               && !message.IsAdd);
            }
        }
    }
}
