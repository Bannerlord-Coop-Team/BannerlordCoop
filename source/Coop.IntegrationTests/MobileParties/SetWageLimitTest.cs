using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
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

        var triggerMessage = new ChangedWagePaymentLimit(party, newValue);

        client1.SimulateMessage(this, triggerMessage);

        // first message sent via game interface
        Assert.Equal(1, client1.InternalMessages.GetMessageCount<ChangedWagePaymentLimit>());


        // verify client sent first message

        Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<NetworkChangeWagePaymentLimitRequest>());
        Assert.Equal(0, client2.NetworkSentMessages.Count); // client 2 should not send any

        // request from client
        Assert.Equal(1, server.InternalMessages.GetMessageCount<NetworkChangeWagePaymentLimitRequest>());


        // all other clients -> NetworkChangeWagePaymentLimit
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeWagePaymentLimit>());
        Assert.Equal(1, client2.InternalMessages.GetMessageCount<NetworkChangeWagePaymentLimit>());
        Assert.Equal(1, client2.InternalMessages.GetMessageCount<WagePaymentApprovedOthers>());


        // server updates itself via -> ChangeWagePaymentLimit
        Assert.Equal(1, server.InternalMessages.GetMessageCount<ChangeWagePaymentLimit>());
    }

}
