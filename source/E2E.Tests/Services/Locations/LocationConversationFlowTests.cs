using Common.Network;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.Locations.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.Players;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

public sealed class LocationConversationFlowTests : MapEventTestBase
{
    public LocationConversationFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void PlayerLocationConversationWaitingOverlay_UsesTheMapWaitingText()
    {
        var viewModel = new LocationPlayerInteractionWaitingOverlayVM("LocationInitiator");

        Assert.Equal("Awaiting proposal from LocationInitiator...", viewModel.WaitingText);
    }

    [Fact]
    public void PlayerLocationConversation_NotifiesReceiverWithoutStartingMapDialog()
    {
        var clients = Clients.Take(2).ToArray();
        var initiatorClient = clients[0];
        var receiverClient = clients[1];

        initiatorClient.Resolve<IControllerIdProvider>().SetControllerId("LocationInitiator");
        receiverClient.Resolve<IControllerIdProvider>().SetControllerId("LocationReceiver");

        CreatePlayerHeroParty("LocationInitiator");
        var (receiverHeroId, receiverMobilePartyId) = CreatePlayerHeroParty("LocationReceiver");
        var receiverPartyId = GetPartyBaseId(receiverMobilePartyId);
        string receiverCharacterId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(receiverHeroId, out var receiverHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(receiverMobilePartyId, out var receiverParty));

            using (new AllowedThread())
            {
                receiverParty.MemberRoster.AddToCounts(receiverHero.CharacterObject, 1);
                receiverHero.PartyBelongedTo = receiverParty;
                receiverParty.ChangePartyLeader(receiverHero);
            }

            if (!Server.ObjectManager.TryGetId(receiverHero.CharacterObject, out receiverCharacterId))
                Assert.True(Server.ObjectManager.AddExisting(receiverCharacterId = "LocationReceiverCharacter", receiverHero.CharacterObject));

            var playerManager = Server.Resolve<IPlayerManager>();
            playerManager.SetPeer("LocationInitiator", initiatorClient.NetPeer);
            playerManager.SetPeer("LocationReceiver", receiverClient.NetPeer);
        }, MapEventDisabledMethods);

        Server.NetworkSentMessages.Clear();
        initiatorClient.Call(() =>
            initiatorClient.Resolve<INetwork>().SendAll(new NetworkRequestLocationConversation(
                "test_location",
                receiverCharacterId,
                generation: 1)));

        var started = Server.NetworkSentMessages.GetMessages<NetworkPlayerInteractionStarted>().Single();
        Assert.Equal(receiverPartyId, started.DefenderPartyId);
        Assert.True(started.IsLocationInteraction);
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAllowLocationConversation>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>());

        Server.NetworkSentMessages.Clear();
        initiatorClient.Call(() => initiatorClient.Resolve<INetwork>().SendAll(new NetworkLocationConversationEnded()));

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerInteractionEnded>().Single();
        Assert.Equal(receiverPartyId, ended.DefenderPartyId);
        Assert.True(ended.IsLocationInteraction);
    }

    private string GetPartyBaseId(string mobilePartyId)
    {
        string partyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetId(mobileParty.Party, out partyId));
        });

        return partyId;
    }
}
