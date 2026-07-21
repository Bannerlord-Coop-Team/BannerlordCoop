using Common;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.Kingdoms.Messages;
using Coop.IntegrationTests.Environment;
using GameInterface.Services.Entity;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Coop.IntegrationTests.Kingdoms;

[Collection(KingdomSyncGameThreadCollection.Name)]
public class KingdomCreationRequestSyncTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    [Fact]
    public void ClientKingdomCreationRequested_Publishes_ServerCommand()
    {
        var client1 = TestEnvironment.Clients.First();
        var server = TestEnvironment.Server;
        client1.Resolve<IControllerIdProvider>().SetControllerId("player1");

        GameThreadTestRunner.Run(() =>
            client1.SimulateMessage(this, new KingdomCreationRequested("Real Kingdom", "empire")));

        Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<NetworkRequestCreateKingdom>());
        Assert.Equal(1, server.InternalMessages.GetMessageCount<NetworkRequestCreateKingdom>());
        Assert.Equal(1, server.InternalMessages.GetMessageCount<CreateKingdom>());

        var networkRequest = Assert.Single(client1.NetworkSentMessages.GetMessages<NetworkRequestCreateKingdom>());
        Assert.Equal("player1", networkRequest.ControllerId);
        Assert.Equal("Real Kingdom", networkRequest.KingdomName);
        Assert.Equal("empire", networkRequest.CultureId);
        Assert.Null(networkRequest.PartyId);
        Assert.Null(networkRequest.SettlementId);

        var serverCommand = Assert.Single(server.InternalMessages.GetMessages<CreateKingdom>());
        Assert.Equal("player1", serverCommand.ControllerId);
        Assert.Equal("Real Kingdom", serverCommand.KingdomName);
        Assert.Equal("empire", serverCommand.CultureId);
    }

    [Fact]
    public void ServerNetworkRequestCreateKingdom_Restores_CreatingPartySettlementContext()
    {
        var server = TestEnvironment.Server;
        var playerManager = server.Resolve<IPlayerManager>();

        var party = server.CreateRegisteredObject<MobileParty>("party1");
        var settlement = server.CreateRegisteredObject<Settlement>("settlement1");
        settlement._partiesCache = new MBList<MobileParty>();
        playerManager.AddPlayer(new Player("player1", "hero1", "party1", "clan1", "character1"));

        Assert.Null(party.CurrentSettlement);

        GameThreadTestRunner.Run(() =>
            server.SimulateMessage(
                this,
                new NetworkRequestCreateKingdom("player1", "Real Kingdom", "empire", "party1", "settlement1")));

        Assert.Same(settlement, party.CurrentSettlement);
        Assert.Single(
            server.NetworkSentMessages.GetMessages<NetworkPartyEnterSettlement>(),
            message => message.PartyId == "party1" && message.SettlementId == "settlement1");
    }

    [Fact]
    public void ServerNetworkRequestCreateKingdom_Publishes_CreateKingdomCommand()
    {
        var server = TestEnvironment.Server;

        GameThreadTestRunner.Run(() =>
            server.SimulateMessage(this, new NetworkRequestCreateKingdom("player1", "Real Kingdom", "empire")));

        var serverCommand = Assert.Single(server.InternalMessages.GetMessages<CreateKingdom>());
        Assert.Equal("player1", serverCommand.ControllerId);
        Assert.Equal("Real Kingdom", serverCommand.KingdomName);
        Assert.Equal("empire", serverCommand.CultureId);
    }

    [Fact]
    public void ServerPlayerKingdomCreated_Broadcasts_NetworkNotification()
    {
        var server = TestEnvironment.Server;

        GameThreadTestRunner.Run(() =>
            server.SimulateMessage(
                this,
                new PlayerKingdomCreated("player1", "Kingdom_Created_1", "Real Kingdom", "Clan_Player")));

        var networkMessage = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkPlayerKingdomCreated>());
        Assert.Equal("player1", networkMessage.ControllerId);
        Assert.Equal("Kingdom_Created_1", networkMessage.KingdomId);
        Assert.Equal("Real Kingdom", networkMessage.KingdomName);
        Assert.Equal("Clan_Player", networkMessage.ClanId);
        Assert.Null(networkMessage.PartyId);
        Assert.Null(networkMessage.SettlementId);

        foreach (var client in TestEnvironment.Clients)
        {
            var localEvent = Assert.Single(client.InternalMessages.GetMessages<PlayerKingdomCreated>());
            Assert.Equal("player1", localEvent.ControllerId);
            Assert.Equal("Kingdom_Created_1", localEvent.KingdomId);
            Assert.Equal("Real Kingdom", localEvent.KingdomName);
            Assert.Equal("Clan_Player", localEvent.ClanId);
        }
    }

    [Fact]
    public void ServerPlayerKingdomCreated_UsesPendingSettlementContextWhenCurrentSettlementWasCleared()
    {
        var server = TestEnvironment.Server;
        var playerManager = server.Resolve<IPlayerManager>();

        var party = server.CreateRegisteredObject<MobileParty>("party1");
        var settlement = server.CreateRegisteredObject<Settlement>("settlement1");
        settlement._partiesCache = new MBList<MobileParty>();
        playerManager.AddPlayer(new Player("player1", "hero1", "party1", "clan1", "character1"));

        GameThreadTestRunner.Run(() =>
            server.SimulateMessage(
                this,
                new NetworkRequestCreateKingdom("player1", "Real Kingdom", "empire", "party1", "settlement1")));

        using (new AllowedThread())
        {
            typeof(MobileParty)
                .GetField("_currentSettlement", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.SetValue(party, null);
        }

        GameThreadTestRunner.Run(() =>
            server.SimulateMessage(
                this,
                new PlayerKingdomCreated("player1", "Kingdom_Created_1", "Real Kingdom", "clan1")));

        var networkMessage = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkPlayerKingdomCreated>());
        Assert.Equal("party1", networkMessage.PartyId);
        Assert.Equal("settlement1", networkMessage.SettlementId);
        Assert.Same(settlement, party.CurrentSettlement);
    }
}

[CollectionDefinition("Kingdom sync game thread", DisableParallelization = true)]
public class KingdomSyncGameThreadCollection
{
    public const string Name = "Kingdom sync game thread";
}
