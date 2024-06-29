using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;
public class ActualClanTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public ActualClanTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
        var party = GameObjectCreator.CreateInitializedObject<MobileParty>();

        var partyId = "PartyId";
        var clanId = "ClanId";

        clan.StringId = clanId;
        party.StringId = partyId;

        foreach (var client in TestEnvironment.Clients)
        {
            client.ObjectManager.AddExisting(party.StringId, GameObjectCreator.CreateInitializedObject<MobileParty>());
            client.ObjectManager.AddExisting(clan.StringId, GameObjectCreator.CreateInitializedObject<Clan>());
        }

        server.ObjectManager.AddExisting(party.StringId, GameObjectCreator.CreateInitializedObject<MobileParty>());
        server.ObjectManager.AddExisting(clan.StringId, GameObjectCreator.CreateInitializedObject<Clan>());

        // Act
        server.Call(() =>
        {
            party.ActualClan = clan;
        });


        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(party.StringId, out var clientParty));
            Assert.Equal(clientParty.ActualClan.StringId, clan.StringId);
        }
    }

    [Fact]
    public void ServerCreateParty_SetNull_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
        var party = GameObjectCreator.CreateInitializedObject<MobileParty>();

        var partyId = "PartyId";
        var clanId = "ClanId";

        clan.StringId = clanId;
        party.StringId = partyId;

        foreach (var client in TestEnvironment.Clients)
        {
            client.ObjectManager.AddExisting(party.StringId, GameObjectCreator.CreateInitializedObject<MobileParty>());
            client.ObjectManager.AddExisting(clan.StringId, GameObjectCreator.CreateInitializedObject<Clan>());
        }

        server.ObjectManager.AddExisting(party.StringId, GameObjectCreator.CreateInitializedObject<MobileParty>());
        server.ObjectManager.AddExisting(clan.StringId, GameObjectCreator.CreateInitializedObject<Clan>());

        // Act
        server.Call(() =>
        {
            party.ActualClan = clan;
            party.ActualClan = null;
        });


        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(party.StringId, out var clientParty));
            Assert.Null(clientParty.ActualClan);
        }
    }

    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;

        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
        var party = GameObjectCreator.CreateInitializedObject<MobileParty>();

        var partyId = "PartyId";
        var clanId = "ClanId";

        clan.StringId = clanId;
        party.StringId = partyId;

        foreach (var client in TestEnvironment.Clients)
        {
            client.ObjectManager.AddExisting(party.StringId, GameObjectCreator.CreateInitializedObject<MobileParty>());
            client.ObjectManager.AddExisting(clan.StringId, GameObjectCreator.CreateInitializedObject<Clan>());
        }

        server.ObjectManager.AddExisting(party.StringId, GameObjectCreator.CreateInitializedObject<MobileParty>());
        server.ObjectManager.AddExisting(clan.StringId, GameObjectCreator.CreateInitializedObject<Clan>());

        // Act

        var executingClient = TestEnvironment.Clients.First();
        executingClient.Call(() =>
        {
            party.ActualClan = clan;
        });


        // Assert
        foreach (var gameInstance in TestEnvironment.Clients.Where(c => c != executingClient).Append(server))
        {
            Assert.True(gameInstance.ObjectManager.TryGetObject<MobileParty>(party.StringId, out var clientParty));
            Assert.NotEqual(clientParty.ActualClan.StringId, clan.StringId);
        }
    }
}
