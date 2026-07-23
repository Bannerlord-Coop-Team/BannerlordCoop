using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;
public class MapEventSideLifetimeTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public MapEventSideLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreate_MapEventSide_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? mapEventId = null;
        server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEventSide>();

            Assert.True(server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        });

        // Assert
        Assert.NotNull(mapEventId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEventSide>(mapEventId, out var _));
        }
    }

    [Fact]
    public void ClientCreate_MapEventSide_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;

        string? mapEventId = null;
        string? mobilePartyId = null;
        server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

            Assert.True(server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.True(server.ObjectManager.TryGetId(mobileParty, out mobilePartyId));
        });

        Assert.NotNull(mapEventId);
        Assert.NotNull(mobilePartyId);

        // Act
        string? clientMapId = null;

        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));

            var mapEventSide = new MapEventSide(mapEvent, BattleSideEnum.Attacker, mobileParty.Party);

            Assert.False(firstClient.ObjectManager.TryGetId(mapEventSide, out clientMapId));
        });

        // Assert
        Assert.Null(clientMapId);
    }

    [Fact]
    public void ServerDestroy_MapEventSide_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? mapEventId = null;
        server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEventSide>();

            Assert.True(server.ObjectManager.TryGetId(mapEvent, out mapEventId));

            mapEvent.HandleMapEventEnd();
        });

        // Assert
        Assert.NotNull(mapEventId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEventSide>(mapEventId, out var _));
        }
    }

    [Fact]
    public void ClientDestroy_MapEventSide_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;

        string? mapEventId = null;
        server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEventSide>();

            Assert.True(server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        });

        Assert.NotNull(mapEventId);

        // Act
        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MapEventSide>(mapEventId, out var mapEvent));

            mapEvent.HandleMapEventEnd();
        });

        // Assert
        Assert.NotNull(mapEventId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEventSide>(mapEventId, out var _));
        }
    }
}
