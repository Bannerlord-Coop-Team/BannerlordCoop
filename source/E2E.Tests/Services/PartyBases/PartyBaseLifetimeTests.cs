using E2E.Tests.Environment;
using E2E.Tests.Util;
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

            party.Party = party.Party;

            Assert.True(server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(server.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });

        // Assert
        Assert.NotNull(partyBaseId);
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(partyBaseId, out var clientPartyBase));
            Assert.Equal(clientParty, clientPartyBase.MobileParty);
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

