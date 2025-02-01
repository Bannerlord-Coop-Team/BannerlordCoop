using Coop.IntegrationTests.Environment.Instance;
using Coop.IntegrationTests.Environment;
using GameInterface.Services.Settlements.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.IntegrationTests.Settlements
{
    /// <summary>
    /// Test syncs for <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent.IsOwnerUnassigned"/>
    /// Verifies all clients receive property changes
    /// </summary>
    public class SettlementComponentIsOwnerUnassignedTest
    {
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();
        [Fact]
        public void ServerSettlementComponentIsOwnerUnassignedChanged_Publishes_AllClients()
        {
            // Arrange
            string settlementId = "SettlementComponent1";
            bool newValue = true;
            var triggerMessage = new SettlementComponentIsOwnerUnassignedChanged(settlementId, newValue);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to it's game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementComponentIsOwnerUnassigned>());

            // Verify the all clients send a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementComponentIsOwnerUnassigned>());
            }
        }
    }
}
