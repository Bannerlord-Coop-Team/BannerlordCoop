using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.IntegrationTests.Settlements;


/// <summary>
/// Used to test that CurrentSiegeState is sent to all clients.
/// </summary>
public class SettlementCurrentSiegeStateTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    [Fact]
    public void ServerCurrentSiegeStateChanged_Publishes_AllClients()
    {
        string settlementId = "Settlement1";
        short siegeState = 1;

        var triggerMessage = new SettlementChangedCurrentSiegeState(settlementId, siegeState);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);


        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementCurrentSiegeState>());

        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementCurrentSiegeState>());
        }
    }
}
