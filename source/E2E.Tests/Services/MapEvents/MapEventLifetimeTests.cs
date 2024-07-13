using E2E.Tests.Environment;
using E2E.Tests.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;
public class MapEventLifetimeTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public MapEventLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreate_MapEvent_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? mapEventId = null;
        server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();

            Assert.True(server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        });

        // Assert
        Assert.NotNull(mapEventId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var _));
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
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();

            Assert.False(firstClient.ObjectManager.TryGetId(mapEvent, out mapEventId));
        });

        // Assert
        Assert.Null(mapEventId);
    }

    [Fact]
    public void ServerDestroy_MapEvent_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? mapEventId = null;
        server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();

            Assert.True(server.ObjectManager.TryGetId(mapEvent, out mapEventId));

            mapEvent.FinalizeEvent();
        });

        // Assert
        Assert.NotNull(mapEventId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var _));
        }
    }

    [Fact]
    public void ClientDestroy_MapEvent_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;

        string? mapEventId = null;
        server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();

            Assert.True(server.ObjectManager.TryGetId(mapEvent, out mapEventId));

            mapEvent.FinalizeEvent();
        });

        Assert.NotNull(mapEventId);

        // Act
        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            Assert.False(firstClient.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));

            mapEvent.FinalizeEvent();
        });

        // Assert
        Assert.NotNull(mapEventId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var _));
        }
    }
}
