using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyBases;

public class PartyBasePropertyTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public PartyBasePropertyTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void Server_MobileParty_SyncAllClients()
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
            var partyBase = new PartyBase(default(MobileParty));

            Assert.True(server.ObjectManager.TryGetId(party.Party, out partyBaseId));


            partyBase.MobileParty = party;
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
    public void Client_MobileParty_DoesNothing()
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
}

