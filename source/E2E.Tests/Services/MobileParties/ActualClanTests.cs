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

        MobileParty? party = null;

        server.Call(() =>
        {
            party = GameObjectCreator.CreateInitializedObject<MobileParty>();
        });


        Assert.NotNull(party);
        Assert.NotNull(party.ActualClan);

        // Act
        Clan? newClan = null;
        server.Call(() =>
        {
            newClan = GameObjectCreator.CreateInitializedObject<Clan>();
            party.ActualClan = newClan;
        });

        Assert.NotNull(newClan);


        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(party.StringId, out var clientParty));
            Assert.Equal(clientParty.ActualClan.StringId, newClan.StringId);
        }
    }

    [Fact]
    public void ServerCreateParty_SetNull_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        MobileParty? party = null;

        server.Call(() =>
        {
            party = GameObjectCreator.CreateInitializedObject<MobileParty>();
        });


        Assert.NotNull(party);
        Assert.NotNull(party.ActualClan);

        // Act
        server.Call(() =>
        {
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

        MobileParty? party = null;

        server.Call(() =>
        {
            party = GameObjectCreator.CreateInitializedObject<MobileParty>();
        });


        Assert.NotNull(party);
        Assert.NotNull(party.ActualClan);

        // Act

        Clan? newClan = null;
        var executingClient = TestEnvironment.Clients.First();
        executingClient.Call(() =>
        {
            newClan = GameObjectCreator.CreateInitializedObject<Clan>();
            party.ActualClan = newClan;
        });

        Assert.NotNull(newClan);

        // Assert
        foreach (var gameInstance in TestEnvironment.Clients.Where(c => c != executingClient).Append(server))
        {
            Assert.True(gameInstance.ObjectManager.TryGetObject<MobileParty>(party.StringId, out var clientParty));
            Assert.NotEqual(clientParty.ActualClan.StringId, newClan.StringId);
        }
    }
}
