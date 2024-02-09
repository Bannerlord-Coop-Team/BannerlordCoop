using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.IntegrationTests.Environment;
using GameInterface.Services.MobileParties.Messages;

namespace Coop.IntegrationTests.MobileParties;

/// <summary>
/// Test Wages changes from client sync
/// </summary>
public class SetWageLimitTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    [Fact]
    public void ServerMobilePartyWageLimitOverride_Publishes_Server_ToClients()
    {
        string mobilePartyId = "MobileParty1";
        int newValue = 20;

        var triggerMessage = new ChangedWagePaymentLimit(mobilePartyId, newValue);

        var client = TestEnvironment.Clients.First();
        var client2 = TestEnvironment.Clients.Last();   
        var server = TestEnvironment.Server;

        client.SimulateMessage(this, triggerMessage);

        // first message sent via game interface
        Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangedWagePaymentLimit>());


        // verify client sent first message

        Assert.Equal(1, client.NetworkSentMessages.GetMessageCount<NetworkChangeWagePaymentLimitRequest>());
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
