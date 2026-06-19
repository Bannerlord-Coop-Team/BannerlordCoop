using Coop.Core.Server.Services.Kingdoms.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;
using TaleWorlds.CampaignSystem.Election;

namespace Coop.IntegrationTests.Kingdoms
{
    /// <summary>
    /// Test class for kingdom decision player vote message handling.
    /// </summary>
    public class KingdomDecisionVoteSyncTest
    {
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        [Fact]
        public void ClientKingdomDecisionVoteRequested_Publishes_ServerCommand()
        {
            var client1 = TestEnvironment.Clients.First();
            var server = TestEnvironment.Server;
            var voteData = new KingdomDecisionVoteData("kingdom1", 0, 1, (int)Supporter.SupportWeights.FullyPush, false);

            client1.SimulateMessage(this, new KingdomDecisionVoteRequested(voteData));

            Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<NetworkRequestKingdomDecisionVote>());
            Assert.Equal(1, server.InternalMessages.GetMessageCount<NetworkRequestKingdomDecisionVote>());
            Assert.Equal(1, server.InternalMessages.GetMessageCount<ChangeKingdomDecisionVote>());
        }

        [Fact]
        public void ServerKingdomDecisionVoteChanged_Publishes_AllClients()
        {
            var server = TestEnvironment.Server;
            var voteData = new KingdomDecisionVoteData("kingdom1", 0, 1, (int)Supporter.SupportWeights.StronglyFavor, false);

            server.SimulateMessage(this, new KingdomDecisionVoteChanged("clan1", voteData));

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeKingdomDecisionVote>());
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<NetworkChangeKingdomDecisionVote>());
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ApplyKingdomDecisionVote>());
            }
        }

        [Fact]
        public void ServerKingdomDecisionResolved_Publishes_AllClients()
        {
            var server = TestEnvironment.Server;
            const string notificationText = "The Western Empire will declare war on the Northern Empire.";

            server.SimulateMessage(this, new KingdomDecisionResolved("kingdom1", 0, 1, true, "OutcomeKey", notificationText));

            var networkMessage = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
            Assert.Equal(notificationText, networkMessage.NotificationText);
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                var receivedMessage = Assert.Single(client.InternalMessages.GetMessages<NetworkKingdomDecisionResolved>());
                Assert.Equal(notificationText, receivedMessage.NotificationText);
                var applyMessage = Assert.Single(client.InternalMessages.GetMessages<ApplyKingdomDecisionResolved>());
                Assert.Equal(notificationText, applyMessage.NotificationText);
            }
        }
    }
}
