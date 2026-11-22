using Coop.Core.Server.Services.Stances.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using Coop.IntegrationTests.Kingdoms;
using GameInterface.Services.Stances.Messages;
using GameInterface.Services.Stances.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Coop.IntegrationTests.Stances
{
    /// <summary>
    /// Test class for war declaration stance sync.
    /// </summary>
    [Collection(KingdomSyncGameThreadCollection.Name)]
    public class FactionDeclareWarSyncTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        [Fact]
        public void ServerFaction_DeclareWarAction_Publishes_AllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;
            var kingdom1 = server.CreateRegisteredObject<Kingdom>("kingdom1");
            var kingdom2 = server.CreateRegisteredObject<Kingdom>("kingdom2");

            // Act
            server.Call(() =>
            {
                DeclareWarActionPatch.Prefix(
                    kingdom1,
                    kingdom2,
                    DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision);
            });

            // Assert
            Assert.Single(server.NetworkSentMessages.GetMessages<NetworkDeclareWar>(),
                message => message.Faction1Id == "kingdom1"
                           && message.Faction2Id == "kingdom2"
                           && message.Detail == (int)DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision);

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Single(client.InternalMessages.GetMessages<DeclareWarChanged>(),
                    message => message.Faction1Id == "kingdom1"
                               && message.Faction2Id == "kingdom2"
                               && message.Detail == (int)DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision);
            }
        }
    }
}
