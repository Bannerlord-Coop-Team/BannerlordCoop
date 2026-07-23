using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;

using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
namespace Coop.IntegrationTests.Settlements;

/// <summary>
/// Test Syncs for Settlement MobilePartyAdd and Remove functions
/// </summary>
public class SettlementMobilePartyCacheTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    /// <summary>
    /// Test Add and Remove mobile parties to settlement cache.
    /// </summary>
    [Fact]
    public void ServerSettlementMobilePartyCacheChanged_Publishes_AllClients()
    {
        var settlement = TestEnvironment.Server.CreateRegisteredObject<Settlement>("settlement1");
        foreach (var client in TestEnvironment.Clients)
        {
            client.CreateRegisteredObject<Settlement>("settlement1");
        }

        var mobileParty = TestEnvironment.Server.CreateRegisteredObject<MobileParty>("mobileParty1");
        foreach (var client in TestEnvironment.Clients)
        {
            client.CreateRegisteredObject<MobileParty>("mobileParty1");
        }

        var triggerMessage = new SettlementChangedMobileParty(settlement, mobileParty, true);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementMobileParty>());
        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeMobileParty>());
        }

    }


}
