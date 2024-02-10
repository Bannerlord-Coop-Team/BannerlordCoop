using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;

namespace Coop.IntegrationTests.Settlement;

/// <summary>
/// Test adding a hero without party
/// </summary>
public class SettlementHeroWithoutPartyTest
{

    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();


    [Fact]
    public void ServerSettlementAddHeroWithoutPartyChanged_Publishes_AllClients()
    {
        string SettlementID = "Settlement1";
        string HeroID = "HERO1";

        var triggerMessage = new SettlementChangedRemoveHeroWithoutParty(SettlementID, HeroID);

        var server = TestEnvironment.Server;
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementRemoveHeroWithoutParty>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementHeroWithoutPartyRemove>());
        }

    }


    [Fact]
    public void ServerSettlementAddRemoveWithoutPartyChanged_Publishes_AllClients()
    {
        string SettlementID = "Settlement1";
        string HeroID = "HERO1";

        var triggerMessage = new SettlementChangedAddHeroWithoutParty(SettlementID, HeroID);

        var server = TestEnvironment.Server;
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementAddHeroWithoutParty>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementHeroWithoutParty>());
        }

    }
}
