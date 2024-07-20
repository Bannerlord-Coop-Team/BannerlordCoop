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
public class SettlementHitPointsRatioAtIndexTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    /// <summary>
    /// Used to Test that client recieves  SetWallSectionHitPointsRatioAtIndex messsages.
    /// </summary>
    [Fact]
    public void ServerSettlementHitPointsRatioAtIndexChanged_Publishes_AllClients()
    {
        string settlementId = "Settlement1";
        int index = 0;
        float hitPointsRatio = 29.0f;

        var triggerMessage = new SettlementWallHitPointsRatioChanged(settlementId, index, hitPointsRatio);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);


        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeWallHitPointsRatio>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementWallHitPointsRatio>());
        }
    }

}
