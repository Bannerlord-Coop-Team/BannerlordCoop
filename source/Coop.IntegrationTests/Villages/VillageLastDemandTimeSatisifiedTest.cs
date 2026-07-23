using Coop.Core.Server.Services.Villages.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Villages.Messages;

using TaleWorlds.CampaignSystem.Settlements;
namespace Coop.IntegrationTests.Villages;

public class VillageLastDemandTimeSatisifiedTest
{

    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();


    /// <summary>
    /// Used to check if LastDemandTimeSatisified server to client works.
    /// </summary>
    [Fact]
    public void ServerLastDemandTimeChanged_Publishes_AllClients()
    {
        // Arrange
        var village = TestEnvironment.Server.CreateRegisteredObject<Village>("village1");
        foreach (var client in TestEnvironment.Clients)
        {
            client.CreateRegisteredObject<Village>("village1");
        }

        var triggerMessage = new VillageDemandTimeChanged(village, 30f);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeVillageDemandTime>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeVillageLastDemandTime>());
        }
    }
}
