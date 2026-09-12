using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ItemRosters.Messages;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyBases;

public class PartyBaseLifetimeTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public PartyBaseLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreate_PartyBase_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        server.NetworkSentMessages.Clear();

        // Act
        string? partyBaseId = null;
        string? partyId = null;
        string? itemRosterId = null;
        server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();

            party.Party = party.Party;

            Assert.True(server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(server.ObjectManager.TryGetId(party.Party, out partyBaseId));
            Assert.True(server.ObjectManager.TryGetId(party.Party.ItemRoster, out itemRosterId));
        });

        // Assert
        Assert.NotNull(partyBaseId);
        Assert.NotNull(partyId);
        Assert.NotNull(itemRosterId);

        var sentMessages = server.NetworkSentMessages.Messages;
        var partyCreateIndex = sentMessages.FindIndex(message => message is NetworkCreateInstance<PartyBase>);
        var itemRosterCreateIndex = sentMessages.FindIndex(message => message is NetworkCreateItemRoster);
        var itemRosterReferenceIndex = sentMessages.FindIndex(message =>
            message.GetType().Name == "PartyBase_ItemRoster_SetNetworkMessage");

        Assert.True(partyCreateIndex >= 0);
        Assert.True(itemRosterCreateIndex > partyCreateIndex);
        Assert.True(itemRosterReferenceIndex > itemRosterCreateIndex);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(partyBaseId, out var clientPartyBase));
            Assert.True(client.ObjectManager.TryGetObject<ItemRoster>(itemRosterId, out var clientItemRoster));
            Assert.Equal(clientParty, clientPartyBase.MobileParty);
            Assert.Same(clientItemRoster, clientPartyBase.ItemRoster);
        }
    }

    [Fact]
    public void ClientCreate_PartyBase_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? partyBaseId = null;

        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            var party = new PartyBase(default(MobileParty));

            Assert.False(server.ObjectManager.TryGetId(party, out partyBaseId));
        });

        // Assert
        Assert.Null(partyBaseId);
    }

    [Fact(Skip = "PartyDestroyed message was removed; needs updating to use current party destruction mechanism")]
    public void ServerDestroy_PartyBase_SyncAllClients() { }

    [Fact(Skip = "PartyDestroyed message was removed; needs updating to use current party destruction mechanism")]
    public void ClientDestroy_PartyBase_DoesNothing() { }
}

