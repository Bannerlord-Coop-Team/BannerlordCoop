using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEngineMissiles;

public class SiegeEngineMissileLifetimeTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public SiegeEngineMissileLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreate_SiegeEngineMissile_SyncAllClients()
    {
        //Arrange
        string? siegeEngineMissileId = null;

        //Act
        Server.Call(() =>
        {
            var siegeEngineMissile = GameObjectCreator.CreateInitializedObject<SiegeEvent.SiegeEngineMissile>();
            Assert.True(Server.ObjectManager.TryGetId(siegeEngineMissile, out siegeEngineMissileId));
        }
        );

        //Assert
        Assert.NotNull(siegeEngineMissileId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent.SiegeEngineMissile>(siegeEngineMissileId, out var _));
        }
    }
    [Fact]
    public void ClientCreate_SiegeEngineMissile_DoesNothing()
    {
        // Arrange

        // Act
        string? clientSiegeEngineMissileId = null;

        var firstClient = TestEnvironment.Clients.First();

        firstClient.Call(() =>
        {
            var siegeEngineType = Game.Current.ObjectManager.GetObject<SiegeEngineType>("catapult");
            var targetEngine = new SiegeEvent.SiegeEngineConstructionProgress(
                                siegeEngineType,
                                1f,
                                100f
                                );
            var missile = new SiegeEvent.SiegeEngineMissile(
                siegeEngineType,
                0,
                SiegeBombardTargets.Wall,
                1,
                targetEngine,
                CampaignTime.Now,
                CampaignTime.Now,
                true
                );

            Assert.False(firstClient.ObjectManager.TryGetId(missile, out clientSiegeEngineMissileId));
        });

        // Assert
        Assert.Null(clientSiegeEngineMissileId);
    }
}
