using Coop.Core.Client.Services.Settlements.Messages;
using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using GameInterface.Services.Settlements.Messages;

namespace Coop.IntegrationTests.Settlements;

/// <summary>
/// LordConversationsCampaignBehavior.conversation_player_ask_to_claim_land_answer_on_consequence() 
/// Settlement.ClaimBy && Settlement.ClaimValue sync transpiler tests
/// </summary>
public class LordConversationsCampaignBehaviorPlayerAskClaim
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

        // server updates itself via 
        Assert.Equal(1, server.InternalMessages.GetMessageCount<ChangeLordConversationCampaignBehaviorPlayerClaim>());
    }

    [Fact]
    public void ClientLordConversationsCampaignBehaviorPlayerAskClaimValueLandOverride_Publishes_Server_ToClients()
    {
        string SettlementId = "S1";
        float newValue = 10.0f;

        var triggerMessage = new LordConversationCampaignBehaviourPlayerChangedClaimValue(SettlementId, newValue);

        var client = TestEnvironment.Clients.First();
        var client2 = TestEnvironment.Clients.Last();
        var server = TestEnvironment.Server;

        client.SimulateMessage(this, triggerMessage);

        Assert.Equal(1, client.InternalMessages.GetMessageCount<LordConversationCampaignBehaviourPlayerChangedClaimValue>());

        // verify client sent first message

        Assert.Equal(1, client.NetworkSentMessages.GetMessageCount<ClientChangeLordConversationCampaignBehaviorPlayerClaimValue>());
        Assert.Equal(0, client2.NetworkSentMessages.Count); // client 2 should not send any

        // request from client
        Assert.Equal(1, server.InternalMessages.GetMessageCount<ClientChangeLordConversationCampaignBehaviorPlayerClaimValue>());

        // other clients
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeLordConverationCampaignBehaviorPlayerClaimValueOther>());
        Assert.Equal(1, client2.InternalMessages.GetMessageCount<NetworkChangeLordConverationCampaignBehaviorPlayerClaimValueOther>());
        Assert.Equal(1, client2.InternalMessages.GetMessageCount<ChangeLordConversationCampaignBehaviorPlayerClaimValueOthers>());

        // server updates itself via
        Assert.Equal(1, server.InternalMessages.GetMessageCount<ChangeLordConversationCampaignBehaviourPlayerClaimValue>());
    }
}
