using Coop.IntegrationTests.Environment.Instance;
using Coop.IntegrationTests.Environment;
using GameInterface.Services.Settlements.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.IntegrationTests.Settlement
{
    /// <summary>
    /// Test syncs for <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.Owner"/>
    /// Verifies all clients receive property changes
    /// </summary>
    public class MilitiaPartyComponentSettlementTest
    {
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();
        [Fact]
        public void ServerSettlementComponentOwnerChanged_Publishes_AllClients()
        {
            // Arrange
            string settlementId = "SettlementComponent1";
            string newOwner = "Owner1";
            var triggerMessage = new SettlementComponentOwnerChanged(settlementId, newOwner);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to it's game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementComponentOwner>());

            // Verify the all clients send a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementComponentOwner>());
            }
        }
    }
}
