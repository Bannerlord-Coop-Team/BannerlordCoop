using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using TaleWorlds.CampaignSystem.Party;
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

        // Act
        string? partyBaseId = null;
        string? partyId = null;
        server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();

            partyId = party.StringId;
            Assert.True(server.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });

        // Assert
        Assert.NotNull(partyBaseId);
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(partyBaseId, out var clientPartyBase));
            Assert.Equal(clientPartyBase.MobileParty, clientParty);
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

    [Fact]
    public void ServerDestroy_PartyBase_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        string? partyId = null;
        string? partyBaseId = null;
        server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();

            Assert.True(server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(server.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });


        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            // PartyBase will be removed with party as they are coupled
            MessageBroker.Instance.Publish(this, new PartyDestroyed(party));
        });

        // Assert
        Assert.NotNull(partyBaseId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<PartyBase>(partyBaseId, out var _));
        }
    }

    [Fact]
    public void ClientDestroy_PartyBase_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;

        string? partyId = null;
        string? partyBaseId = null;
        server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();

            Assert.True(server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(server.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });


        // Act

        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            // PartyBase will be removed with party as they are coupled
            MessageBroker.Instance.Publish(this, new PartyDestroyed(party));
        });

        // Assert
        Assert.NotNull(partyBaseId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(partyBaseId, out var _));
        }
    }
}

