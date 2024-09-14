using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Workshops;

public class WorkshopCreationTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public WorkshopCreationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateWorkshop_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? WorkshopId = null;
        server.Call(() =>
        {
            Settlement settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var Workshop = new Workshop(settlement, "test");
            Assert.True(server.ObjectManager.TryGetId(Workshop, out WorkshopId));
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var _));
        }
    }

    [Fact]
    public void ClientCreateWorkshop_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();
        // Act
        string? WorkshopId = null;
        client1.Call(() =>
        {
            Settlement settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var Workshop = new Workshop(settlement, "test");
            Assert.False(client1.ObjectManager.TryGetId(Workshop, out WorkshopId));
        });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var _));
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var _));
        }
    }
}