using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.IntegrationTests.Settlement
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
            string settlementId = "SettlementComponent1";
            int newGold = 120;
            var triggerMessage = new SettlementComponentGoldChanged(settlementId, newGold);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to it's game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementComponentGold>());

            // Verify the all clients send a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementComponentGold>());
            }
        }
    }
}
