using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Villages;

public class VillageLifetimeTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public VillageLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateVillage_SyncAllClients()
    {
        // Arrange
        string? villageId = null;

        // Act
        Server.Call(() =>
        {
            var village = GameObjectCreator.CreateInitializedObject<Village>();
            Assert.True(Server.ObjectManager.TryGetId(village, out villageId));
        });

        // Assert
        Assert.NotNull(villageId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Village>(villageId, out var _));
        }
    }

    [Fact]
    public void ClientCreateVillage_DoesNothing()
    {
        // Arrange
        string? clientVillageId = null;

        // Act
        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            var village = ObjectHelper.SkipConstructor<Village>();

            Assert.False(firstClient.ObjectManager.TryGetId(village, out clientVillageId));
        });

        // Assert
        Assert.Null(clientVillageId);
    }
}

