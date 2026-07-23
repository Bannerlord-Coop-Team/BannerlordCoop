using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventParties;
public class MapEventPartyLifetimeTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public MapEventPartyLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreate_MapEventParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? mapEventId = null;
        server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEventParty>();

            Assert.True(server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        });

        // Assert
        Assert.NotNull(mapEventId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEventParty>(mapEventId, out var _));
        }
    }

    [Fact]
    public void ClientCreate_MapEvent_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var firstClient = TestEnvironment.Clients.First();

        // MapEventParty's constructor dereferences the PartyBase, so build a valid party on the
        // server (synced to clients) and construct from it on the client - mirrors MapEventPartyBuilder.
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Act
        string? mapEventId = null;
        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(partyId, out var mobileParty));

            var mapEvent = new MapEventParty(mobileParty.Party);

            Assert.False(firstClient.ObjectManager.TryGetId(mapEvent, out mapEventId));
        });

        // Assert
        Assert.Null(mapEventId);
    }
}