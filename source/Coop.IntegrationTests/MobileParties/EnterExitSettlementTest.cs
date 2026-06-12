using Common;
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
            // verifies the PartyEnterSettlement message reaches them, and resolving the objects
            // there would run EnterSettlementActionPatches.OverrideApplyForParty, whose blocking
            // RunOnMainThread call cannot complete in this environment (no game loop is pumping).
            var party = client1.CreateRegisteredObject<MobileParty>("party1");
            var settlement = client1.CreateRegisteredObject<Settlement>("settlement1");

            var message = new StartSettlementEncounterAttempted(party, settlement);

            // Act
            client1.SimulateMessage(this, message);

            // Assert
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
                Assert.Equal(1, client.InternalMessages.GetMessageCount<PartyLeaveSettlement>());
            }
        }
    }
}
