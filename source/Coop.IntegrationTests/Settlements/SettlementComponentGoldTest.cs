using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.IntegrationTests.Settlements
{
    /// <summary>
    /// Test syncs for <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.Gold"/>
    /// Verifies all clients receive property changes
    /// </summary>
    public class SettlementComponentGoldTest
    {
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();
        [Fact]
        public void ServerSettlementComponentGoldChanged_Publishes_AllClients()
        {
            // Arrange
            //string settlementId = "SettlementComponent1";
            var settlementId = "MySettlement";
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            TestEnvironment.RegisterObjectInNetwork(settlement, settlementId);

            var settlementComponent = ObjectHelper.SkipConstructor<Hideout>();
            TestEnvironment.RegisterObjectInNetwork<SettlementComponent>(settlementComponent);

            int newGold = 120;
            var triggerMessage = new SettlementComponentGoldChanged(settlementComponent, newGold);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // The update is coalesced, so it only reaches clients once the server flushes for the tick.
            FlushCoalescer(server);

            // Assert
            // Verify the server sends a single message to it's game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementComponentGold>());

            // Verify the all clients send a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementComponentGold>());
            }
        }

        /// <summary>
        /// Repeated gold changes to the same component within a tick collapse into one latest-wins send.
        /// </summary>
        [Fact]
        public void ServerCoalescesSettlementComponentGold_SendsLatestOnly()
        {
            // Arrange
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            TestEnvironment.RegisterObjectInNetwork(settlement, "MySettlement");

            var settlementComponent = ObjectHelper.SkipConstructor<Hideout>();
            TestEnvironment.RegisterObjectInNetwork<SettlementComponent>(settlementComponent);

            var server = TestEnvironment.Server;

            // Act: two gold changes for the same component, the latest value being 999.
            server.SimulateMessage(this, new SettlementComponentGoldChanged(settlementComponent, 120));
            server.SimulateMessage(this, new SettlementComponentGoldChanged(settlementComponent, 999));

            // Nothing goes out until the tick flush.
            Assert.Equal(0, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementComponentGold>());

            FlushCoalescer(server);

            // Assert: the two changes collapse into one send carrying the latest gold.
            var sent = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkChangeSettlementComponentGold>());
            Assert.Equal(999, sent.Gold);

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                var update = Assert.Single(client.InternalMessages.GetMessages<ChangeSettlementComponentGold>());
                Assert.Equal(999, update.Gold);
            }
        }

        // Drains the server's per-tick coalescer the way CoopServer.Update does, inside the server's
        // static scope so the merged send routes to clients.
        private static void FlushCoalescer(EnvironmentInstance server)
        {
            server.Call(() => server.Resolve<ISendCoalescer>().Flush(server.Resolve<INetwork>()));
        }
    }
}
