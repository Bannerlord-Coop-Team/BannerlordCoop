using Common.Util;
using Coop.IntegrationTests.Environment;
using GameInterface.Services.MobileParties.Messages;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.IntegrationTests.MobileParties;

/// <summary>
/// Test Wages changes from client sync
/// </summary>
public class SetWageLimitTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    [Fact]
    public void ClientMobilePartyWageLimitOverride_Publishes_Server_ToClients()
    {
        int newValue = 20;

        var mobilePartyId = "MyParty";

        var client1 = TestEnvironment.Clients.First();
        var client2 = TestEnvironment.Clients.Last();   
        var server = TestEnvironment.Server;

        server.CreateRegisteredObject<MobileParty>(mobilePartyId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.CreateRegisteredObject<MobileParty>(mobilePartyId);
        }

        Assert.True(client1.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));

        var triggerMessage = new WagePaymentLimitSet(party, newValue);

        client1.SimulateMessage(this, triggerMessage);

        // Verify message sent from GameInterface
        Assert.Equal(1, client1.InternalMessages.GetMessageCount<WagePaymentLimitSet>());

        // Verify client sent first message and other client didn't send a message
        Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<SetWagePaymentLimit>());
        Assert.Equal(0, client2.NetworkSentMessages.Count);

        // Verify server received message from client
        Assert.Equal(1, server.InternalMessages.GetMessageCount<SetWagePaymentLimit>());
    }

}
