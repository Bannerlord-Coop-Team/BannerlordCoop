using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;

using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
namespace Coop.IntegrationTests.Settlements;

/// <summary>
/// Used to test that the LastAttackerParty is sent to client.
/// </summary>
public class SettlementLastAttackerPartyTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    [Fact]
    public void ServerLastAttackerPartyChanged_Publishes_AllClients()
    {
        var settlement = TestEnvironment.Server.CreateRegisteredObject<Settlement>("settlement1");
        foreach (var client in TestEnvironment.Clients)
        {
            client.CreateRegisteredObject<Settlement>("settlement1");
        }

        var attackerParty = TestEnvironment.Server.CreateRegisteredObject<MobileParty>("attackerParty1");
        foreach (var client in TestEnvironment.Clients)
        {
            client.CreateRegisteredObject<MobileParty>("attackerParty1");
        }

        var triggerMessage = new SettlementChangedLastAttackerParty(settlement, attackerParty);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementLastAttackerParty>());

        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementLastAttackerParty>());
        }
    }
}
