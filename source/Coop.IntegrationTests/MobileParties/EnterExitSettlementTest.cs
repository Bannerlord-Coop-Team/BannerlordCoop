using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.IntegrationTests.Environment;
using GameInterface.Services.MobileParties.Messages.Behavior;

using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
namespace Coop.IntegrationTests.MobileParties
{
    public class EnterExitSettlementTest
    {
        internal TestEnvironment TestEnvironment { get; }

        public EnterExitSettlementTest()
        {
            // Creates a test environment with 1 server and 2 clients by default
            TestEnvironment = new TestEnvironment();
        }

        /// <summary>
        /// Verify sending StartSettlementEncounterAttempted on one client
        /// Triggers PartyEnterSettlement on all other clients
        /// </summary>
        [Fact]
        public void EnterSettlement_Publishes_AllClients()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();

            // The message is published on client1, so its handler resolves the objects
            // through client1's object manager - the message must carry client1's instances.
            // The objects are intentionally NOT registered on the other clients: this test only
            // verifies the NetworkPartyEnterSettlement message reaches them. Resolving the objects
            // there would invoke ISettlementInterface.PartyEnterSettlement, whose blocking
            // GameThread.Run call cannot complete in this environment (no game loop is pumping) -
            // the unregistered objects make the handler's lookup fail first, so it is never invoked.
            var party = client1.CreateRegisteredObject<MobileParty>("party1");
            var settlement = client1.CreateRegisteredObject<Settlement>("settlement1");

            var message = new StartSettlementEncounterAttempted(party, settlement);

            // Act
            client1.SimulateMessage(this, message);

            // Assert
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<NetworkPartyEnterSettlement>());
            }
        }

        /// <summary>
        /// While the first settlement-encounter request is still in flight, the controlled party re-attempts the
        /// encounter every campaign tick. Verify those rapid retries are rate-limited to a single network request
        /// instead of flooding the server.
        /// </summary>
        [Fact]
        public void RapidEnterAttempts_RateLimited_ToOneRequest()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();

            var party = client1.CreateRegisteredObject<MobileParty>("party1");
            var settlement = client1.CreateRegisteredObject<Settlement>("settlement1");

            var message = new StartSettlementEncounterAttempted(party, settlement);

            // Act - two attempts in immediate succession, well within the request cooldown
            client1.SimulateMessage(this, message);
            client1.SimulateMessage(this, message);

            // Assert - only the first attempt reaches the server; the second is dropped by the rate limiter
            Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<NetworkRequestStartSettlementEncounter>());

            // And the enter is applied on the other clients exactly once
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<NetworkPartyEnterSettlement>());
            }
        }

        /// <summary>
        /// While the first settlement-encounter request is still in flight, the controlled party re-attempts the
        /// encounter every campaign tick. Verify those rapid retries are rate-limited to a single network request
        /// instead of flooding the server.
        /// </summary>
        [Fact]
        public void RapidEnterAttempts_RateLimited_ToOneRequest()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();

            var party = client1.CreateRegisteredObject<MobileParty>("party1");
            var settlement = client1.CreateRegisteredObject<Settlement>("settlement1");

            var message = new StartSettlementEncounterAttempted(party, settlement);

            // Act - two attempts in immediate succession, well within the request cooldown
            client1.SimulateMessage(this, message);
            client1.SimulateMessage(this, message);

            // Assert - only the first attempt reaches the server; the second is dropped by the rate limiter
            Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<NetworkRequestStartSettlementEncounter>());

            // And the enter is applied on the other clients exactly once
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<PartyEnterSettlement>());
            }
        }

        /// <summary>
        /// Verify sending StartSettlementEncounterAttempted on one client
        /// Triggers PartyLeaveSettlement on all other clients
        /// </summary>
        [Fact]
        public void LeaveSettlement_Publishes_AllClients()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();

            TestEnvironment.Server.CreateRegisteredObject<MobileParty>("party1");
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                client.CreateRegisteredObject<MobileParty>("party1");
            }

            // The message is published on client1, so its handler resolves the party
            // through client1's object manager - the message must carry client1's instance.
            var party = client1.CreateRegisteredObject<MobileParty>("party1");

            var message = new EndSettlementEncounterAttempted(party);

            // Act
            client1.SimulateMessage(this, message);

            // Assert
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<NetworkPartyLeaveSettlement>());
            }
        }
    }
}
