using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Client.Services.Settlements.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.Settlements.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.IntegrationTests.Settlement;
public class LordConversationsCampaignBehaviorPlayerAskClaimLand
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    [Fact]
    public void ClientLordConversationsCampaignBehaviorPlayerAskClaimLandOverride_Publishes_Server_ToClients()
    {
        string SettlementId = "S1";
        string HeroId = "H1";

        var triggerMessage = new LordConversationCampaignBehaviourPlayerChangedClaim(SettlementId, HeroId);

        var client = TestEnvironment.Clients.First();
        var client2 = TestEnvironment.Clients.Last();
        var server = TestEnvironment.Server;

        client.SimulateMessage(this, triggerMessage);

        Assert.Equal(1, client.InternalMessages.GetMessageCount<LordConversationCampaignBehaviourPlayerChangedClaim>());

        // verify client sent first message

        Assert.Equal(1, client.NetworkSentMessages.GetMessageCount<ClientChangeLordConversationCampaignBehaviorPlayerClaim>());
        Assert.Equal(0, client2.NetworkSentMessages.Count); // client 2 should not send any

        // request from client
        Assert.Equal(1, server.InternalMessages.GetMessageCount<ClientChangeLordConversationCampaignBehaviorPlayerClaim>());

        // other clients
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeLordConverationCampaignBehaviorPlayerClaimOther>());
        Assert.Equal(1, client2.InternalMessages.GetMessageCount<NetworkChangeLordConverationCampaignBehaviorPlayerClaimOther>());
        Assert.Equal(1, client2.InternalMessages.GetMessageCount<ChangeLordConversationCampaignBehaviorPlayerClaimOthers>());

        // server updates itself via -> ChangeWagePaymentLimit
        Assert.Equal(1, server.InternalMessages.GetMessageCount<ChangeLordConversationCampaignBehaviorPlayerClaim>());
    }
}
