using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.MapEvents;
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

        // Act
        string? mapEventId = null;

        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            var mapEvent = new MapEventParty(default);

            Assert.False(firstClient.ObjectManager.TryGetId(mapEvent, out mapEventId));
        });

        // Assert
        Assert.Null(mapEventId);
    }
}